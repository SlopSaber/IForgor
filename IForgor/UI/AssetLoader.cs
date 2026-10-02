using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using IPA.Utilities.Async;
using UnityEngine;

namespace IForgor.UI
{
    internal class AssetLoader
    {
        private static readonly Lazy<Task<byte[][]>> ResourceBytes = new Lazy<Task<byte[][]>>(() =>
        {
            Assembly assembly = typeof(AssetLoader).Assembly;
            return Task.Run(() => ReadResourceBytes(assembly));
        });

        private volatile bool _terminated;

        public Sprite spr_bloq { get; private set; }
        public Sprite spr_slider { get; private set; }
        public Sprite spr_arrow { get; private set; }
        public Sprite spr_dot { get; private set; }
        public Sprite spr_slider_dots { get; private set; }
        public Sprite spr_cut_arrow { get; private set; }
        public Sprite spr_saber_bg { get; private set; }
        public Sprite spr_saber_fg { get; private set; }

        public Material mat_UINoGlow { get; private set; }
        internal bool IsComplete { get; private set; }
        internal Exception LoadError { get; private set; }
        internal event Action Completed;

        internal static void PrepareResources()
        {
            _ = ResourceBytes.Value;
        }

        public AssetLoader()
        {
            Application.quitting += OnApplicationQuit;
            try
            {
                Task<byte[][]> preparation = ResourceBytes.Value;
                if (preparation.IsCompleted)
                {
                    CreateAssets(preparation.GetAwaiter().GetResult());
                    IsComplete = true;
                }
                else
                {
                    _ = CompleteAssetsAsync(preparation);
                }
            }
            catch
            {
                OnApplicationQuit();
                throw;
            }
        }

        private async Task CompleteAssetsAsync(Task<byte[][]> preparation)
        {
            byte[][] data = null;
            Exception error = null;
            try
            {
                data = await preparation.ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                error = ex;
            }

            try
            {
                await UnityMainThreadTaskScheduler.Factory.StartNew(() =>
                {
                    if (_terminated) return;
                    try
                    {
                        if (error != null) throw error;
                        CreateAssets(data);
                    }
                    catch (Exception ex)
                    {
                        LoadError = ex;
                        Plugin.Log.Error($"Unable to load pause icons: {ex}");
                    }
                    IsComplete = true;
                    Completed?.Invoke();
                }).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                if (!_terminated) Plugin.Log.Error($"Unable to publish pause icons: {ex}");
            }
        }

        private void OnApplicationQuit()
        {
            _terminated = true;
            Completed = null;
            Application.quitting -= OnApplicationQuit;
        }

        private void CreateAssets(byte[][] data)
        {
            spr_bloq = CreateSprite(data[0]);
            spr_slider = CreateSprite(data[1]);
            spr_arrow = CreateSprite(data[2]);
            spr_dot = CreateSprite(data[3]);
            spr_slider_dots = CreateSprite(data[4]);
            spr_cut_arrow = CreateSprite(data[5]);
            spr_saber_bg = CreateSprite(data[6]);
            spr_saber_fg = CreateSprite(data[7]);

            mat_UINoGlow = new Material(Resources.FindObjectsOfTypeAll<Material>().Where(m => m.name == "UINoGlow").First())
            {
                name = "UINoGlowEvenMoreCustomThanBSMLLOLnojk"
            };
        }

        private static byte[][] ReadResourceBytes(Assembly assembly)
        {
            string[] paths =
            {
                "IForgor.Resources.bloq.png", "IForgor.Resources.slider.png",
                "IForgor.Resources.arrow.png", "IForgor.Resources.dot.png",
                "IForgor.Resources.slider_dots.png", "IForgor.Resources.cut_arrow.png",
                "IForgor.Resources.saber_bg.png", "IForgor.Resources.saber_fg.png"
            };
            byte[][] result = new byte[paths.Length][];
            for (int i = 0; i < paths.Length; i++)
            {
                using Stream stream = assembly.GetManifestResourceStream(paths[i]);
                if (stream == null) continue;
                byte[] data = new byte[stream.Length];
                stream.Read(data, 0, (int)stream.Length);
                result[i] = data;
            }
            return result;
        }

        private static Sprite CreateSprite(byte[] data)
        {
            if (data == null) return null;
            Texture2D tex = new Texture2D(2, 2);
            if (tex.LoadImage(data))
                return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0, 0), 100);
            return null;
        }
    }
}
