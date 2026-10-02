using System;
using System.Linq;
using HMUI;
using IForgor.Recorders;
using IPA.Utilities;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace IForgor.UI
{
	internal class PauseUIManager : IInitializable, IDisposable
	{
		private readonly AssetLoader _assetLoader;
		private readonly NoteRecorder _noteRecorder;
		private readonly SaberRecorder _saberRecorder;
		private readonly DiContainer _container;

		private Transform _pauseCanvasTransform;
		private ColorManager _colorManager;
		private PauseController _pauseController;

		private bool _groupANullified = true;
		private bool _groupBNullified = true;
		private bool _uiCreated;
		private bool _disposed;
		private PauseSnapshot _pendingPause;

		private sealed class PauseSnapshot
		{
			internal readonly NoteData NoteA;
			internal readonly NoteData NoteB;
			internal readonly NoteCutInfo? CutA;
			internal readonly NoteCutInfo? CutB;
			internal readonly float SaberA;
			internal readonly float SaberB;

			internal PauseSnapshot(NoteData noteA, NoteData noteB, NoteCutInfo? cutA,
				NoteCutInfo? cutB, float saberA, float saberB)
			{
				NoteA = noteA;
				NoteB = noteB;
				CutA = cutA;
				CutB = cutB;
				SaberA = saberA;
				SaberB = saberB;
			}
		}

		public UIGroup groupA;
		public UIGroup groupB;

		public PauseUIManager(AssetLoader assetLoader, NoteRecorder noteRecorder, SaberRecorder saberRecorder,
			DiContainer container, ColorManager colorManager, PauseController pauseController)
		{
			_assetLoader = assetLoader;
			_noteRecorder = noteRecorder;
			_saberRecorder = saberRecorder;
			_container = container;
			_colorManager = colorManager;
			_pauseController = pauseController;
		}
		
		public void Initialize() {
			_pauseController.didPauseEvent += OnPause;
			if (_assetLoader.IsComplete) OnAssetsCompleted();
			else _assetLoader.Completed += OnAssetsCompleted;
		}

		public void Dispose() {
			_disposed = true;
			_pauseController.didPauseEvent -= OnPause;
			_assetLoader.Completed -= OnAssetsCompleted;
			_pendingPause = null;
		}

		private void OnAssetsCompleted()
		{
			_assetLoader.Completed -= OnAssetsCompleted;
			if (_disposed || !_pauseController) return;
			if (_assetLoader.LoadError != null)
			{
				_pauseController.didPauseEvent -= OnPause;
				_pendingPause = null;
				return;
			}
			try
			{
				CreateUIElements();
				_uiCreated = true;
				if (_pendingPause != null) ApplyPauseSnapshot(_pendingPause);
				_pendingPause = null;
			}
			catch
			{
				_pauseController.didPauseEvent -= OnPause;
				_pendingPause = null;
				throw;
			}
		}

		private void CreateUIElements() {
			if (_pauseCanvasTransform == null)
				_pauseCanvasTransform = Resources.FindObjectsOfTypeAll<PauseMenuManager>().FirstOrDefault().transform.Find("Wrapper").Find("MenuWrapper").Find("Canvas").transform;

			Sprite spr_RoundRect10 = _pauseCanvasTransform.Find("MainBar").Find("LevelBarSimple").Find("BG").GetComponent<ImageView>().sprite;

			RectTransform uiContainer = new GameObject("IFUIContainer", typeof(RectTransform)).GetComponent<RectTransform>();
			uiContainer.SetParent(_pauseCanvasTransform, false);
			uiContainer.localScale = Vector3.one;
			uiContainer.localPosition = Vector3.zero;
			uiContainer.sizeDelta = new Vector2(3.0f, 10.0f);

			ImageView background = new GameObject("IFUIBackground").AddComponent<ImageView>();
			background.transform.SetParent(uiContainer.transform, false);
			background.rectTransform.localScale = new Vector3(1.0f, 1.0f, 1.0f);
			background.rectTransform.localPosition = new Vector3(0.0f, 14.0f, 0.0f);
			background.rectTransform.sizeDelta = new Vector2(40.0f, 10.0f);
			background.sprite = spr_RoundRect10;
			background.type = Image.Type.Sliced;
			background.color = new Color(0.125f, 0.125f, 0.125f, 0.75f);
			background.material = _assetLoader.mat_UINoGlow;
			background.SetField<ImageView, float>("_skew", 0.18f);
			background.SetAllDirty();

			groupA = _container.InstantiateComponentOnNewGameObject<UIGroup>("IFUIBloq_TypeA");
			groupB = _container.InstantiateComponentOnNewGameObject<UIGroup>("IFUIBloq_TypeB");

			groupA.Initialize();
			groupB.Initialize();

			groupA.SetNoteColor(_colorManager.ColorForType(ColorType.ColorA));
			groupB.SetNoteColor(_colorManager.ColorForType(ColorType.ColorB));
			groupA.SetSaberColor(_colorManager.ColorForSaberType(SaberType.SaberA));
			groupB.SetSaberColor(_colorManager.ColorForSaberType(SaberType.SaberB));

			groupA.SetSaberXPosition(-9.0f);
			groupB.SetSaberXPosition(9.0f);

			Transform groupATransform  = groupA.transform;
			groupATransform.SetParent(background.transform, false);
			groupATransform.localPosition = new Vector3(-5.0f, 0.0f, 0.0f);
			Transform groupBTransform  = groupB.transform;
			groupBTransform.SetParent(background.transform, false);
			groupBTransform.localPosition = new Vector3(5.0f, 0.0f, 0.0f);
		}

		private void OnPause() {
			if (_disposed) return;
			if (!_uiCreated)
			{
				if (_assetLoader.LoadError != null) return;
				_saberRecorder.RecordSaberAngles();
				_pendingPause = new PauseSnapshot(_noteRecorder.noteAData, _noteRecorder.noteBData,
					_noteRecorder.noteACutInfo, _noteRecorder.noteBCutInfo,
					_saberRecorder.saberAAngle, _saberRecorder.saberBAngle);
				return;
			}
			ApplyPauseSnapshot(null);
		}

		private void ApplyPauseSnapshot(PauseSnapshot snapshot)
		{
			if (_disposed || !groupA || !groupB) return;
			NoteData noteA = snapshot == null ? _noteRecorder.noteAData : snapshot.NoteA;
			NoteData noteB = snapshot == null ? _noteRecorder.noteBData : snapshot.NoteB;
			if (noteA != null) {
				if (_groupANullified && _colorManager != null) {
					groupA.SetNoteColor(_colorManager.ColorForType(ColorType.ColorA));
				}
				_groupANullified = false;
				groupA.SetNoteData(noteA);
			} else {
				if (_colorManager != null) {
					groupA.SetNoteColor(Color.gray);
				}
				_groupANullified = true;
			}
			if (noteB != null) {
				if (_groupBNullified && _colorManager != null) {
					groupB.SetNoteColor(_colorManager.ColorForType(ColorType.ColorB));
				}
				_groupBNullified = false;
				groupB.SetNoteData(noteB);
			} else {
				if (_colorManager != null) {
					groupB.SetNoteColor(Color.gray);
				}
				_groupBNullified = true;
			}

			groupA.SetNoteCutInfo(snapshot == null ? _noteRecorder.noteACutInfo : snapshot.CutA);
			groupB.SetNoteCutInfo(snapshot == null ? _noteRecorder.noteBCutInfo : snapshot.CutB);

			if (snapshot == null) _saberRecorder.RecordSaberAngles();
			groupA.SetSaberAngle(snapshot == null ? _saberRecorder.saberAAngle : snapshot.SaberA);
			groupB.SetSaberAngle(snapshot == null ? _saberRecorder.saberBAngle : snapshot.SaberB);
		}
	}
}
