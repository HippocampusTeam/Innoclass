﻿using System;
using System.Collections.Generic;
using Simulation;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Editor {
public class SimulationWindow : EditorWindow {
	private const string windowTitle = "Симуляция";
	private const string tracksHintText = "Создайте два или больше своих уровней - проявите креативность!";
	private const string modelsHintText = "Это ваши модели. Выбирайте те, которые хотите тренировать и загружайте карту. Натренируйте модели разнообразно";
	private const string pickModelsErrorText = "Для тренеровки выберете одну или две модели";
	private const int editorGap = 3;
	private const int trackButtonBaseWidth = 240;
	private const int trackButtonIconWidth = 68;
	private const int trackButtonFullWidth = trackButtonBaseWidth + trackButtonIconWidth;
	private const int speedButtonWidth = 24;

	private static readonly int[] speedFactors = { 1, 2, 4 };

	private void OnGUI() {
		titleContent = new GUIContent(windowTitle);
		GUILayout.BeginHorizontal();
		showStoryTracksGUI();
		showUgcTracksGUI();
		showMiscGUI();
		showSpeedGUI();
		GUILayout.EndHorizontal();
	}

	private static void showStoryTracksGUI() {
		GUILayout.BeginVertical(GUILayout.MaxWidth(trackButtonFullWidth + editorGap));
		GUILayout.Label("Подготовленные карты:", EditorStyles.boldLabel);

		IEnumerable<string> tracksNames = TracksManager.getTracksNamesOfType(TracksManager.TrackType.story);
		foreach (string trackName in tracksNames)
			if (GUILayout.Button(new GUIContent(trackName), GUILayout.MaxWidth(trackButtonFullWidth)))
				TracksManager.openTrack(trackName, TracksManager.TrackType.story);

		GUILayout.EndVertical();
	}

	private static void showUgcTracksGUI() {
		GUILayout.BeginVertical(GUILayout.MaxWidth(trackButtonFullWidth + editorGap));
		GUILayout.Label("Ваши карты:", EditorStyles.boldLabel);
		
		EditorGUILayout.HelpBox(new GUIContent(tracksHintText));

		IEnumerable<string> tracksNames = TracksManager.getTracksNamesOfType(TracksManager.TrackType.ugc);
		foreach (string trackName in tracksNames) {
			GUILayout.BeginHorizontal();
			if (GUILayout.Button(trackName, GUILayout.MaxWidth(trackButtonBaseWidth)))
				TracksManager.openTrack(trackName, TracksManager.TrackType.ugc);
			if (GUILayout.Button("Удалить", GUILayout.MaxWidth(trackButtonIconWidth - editorGap)))
				TracksManager.deleteTrack(trackName);
			GUILayout.EndHorizontal();
		}

		if (GUILayout.Button(new GUIContent("Создать новую карту"), GUILayout.MaxWidth(trackButtonFullWidth)))
			TracksManager.openTrack(TracksManager.createNewTrack(), TracksManager.TrackType.ugc);

		GUILayout.EndVertical();
	}

	private static void showMiscGUI() {
		GUILayout.BeginVertical(GUILayout.MaxWidth(trackButtonFullWidth + editorGap));
		GUILayout.Label("Ваше Имя:", EditorStyles.boldLabel);

		string newUsername = EditorGUILayout.TextField(UserManager.username, GUILayout.ExpandWidth(false));
		if (newUsername != null) UserManager.username = newUsername;

		if (GUILayout.Button(new GUIContent("Синхронизировать код"), GUILayout.MaxWidth(trackButtonFullWidth)))
			PythonCodeSyncer.sync();
		
		// Possibly execute showTopologyControlGUI() or showModelsControlGUI() here
		
		GUILayout.EndVertical();
	}

	private static void showModelsControlGUI() {
		GUILayout.Label("Ваши Модели:", EditorStyles.boldLabel);

		EditorGUILayout.HelpBox(new GUIContent(modelsHintText));
		if (!ModelsManager.getInstance().isNumberOfActiveModelsValid()) EditorGUILayout.HelpBox(pickModelsErrorText, MessageType.Error);

		foreach (SimulationModel model in ModelsManager.getInstance().models) {
			bool newIsActivated = GUILayout.Toggle(model.isActivated, new GUIContent(model.name));
			if (newIsActivated == model.isActivated) continue;
			model.isActivated = newIsActivated;
			ModelsManager.getInstance().saveEverything();
		}
	}
	
	private static void showSpeedGUI() {
		if (SceneManager.GetActiveScene().name.StartsWith("11")) return;
		
		GUILayout.BeginVertical(GUILayout.MaxWidth((speedButtonWidth + editorGap) * speedFactors.Length - editorGap));
		GUILayout.Label("Ускорение:", EditorStyles.boldLabel);
		
		GUILayout.BeginHorizontal();
		foreach (int factor in speedFactors)
			if (GUILayout.Button(new GUIContent("x" + factor), GUILayout.MaxWidth(speedButtonWidth)))
				SpeedManager.setSpeed(factor);
		GUILayout.EndHorizontal();
		
		showControlModeGUI();
		
		GUILayout.EndVertical();
	}
	
	private static void showControlModeGUI() {
		// GUILayout.BeginVertical(GUILayout.MaxWidth((speedButtonWidth + editorGap) * speedFactors.Length - editorGap));
		GUILayout.Label("Режим управления:", EditorStyles.boldLabel);

		bool newControlType = EditorGUILayout.Toggle(new GUIContent("Ручной"), UserManager.userControl);
		if (UserManager.userControl != newControlType) UserManager.userControl = newControlType;
		
		// GUILayout.EndVertical();
	}

	private static void showTopologyControlGUI() {
		GUILayout.Label("Топология:", EditorStyles.boldLabel);

		GUILayout.BeginHorizontal();
		uint[] layers = ModelsManager.getInstance().topology;
		for (int i = 0; i < layers.Length; i++) {
			uint newLayerSize = Convert.ToUInt32(EditorGUILayout.IntField(Convert.ToInt32(layers[i]), GUILayout.ExpandWidth(false)));
			if (newLayerSize == layers[i]) continue;
			ModelsManager.getInstance().topology[i] = newLayerSize;
			ModelsManager.getInstance().saveTopology();
		}
		GUILayout.EndHorizontal();
	}
}
}