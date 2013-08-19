﻿using UnityEditor;
using UnityEngine;

using System;
using System.Collections;
using System.IO;

abstract public class CreateVolumeWizard : ScriptableWizard
{
	protected string datasetName = "New Volume";
	
	protected int width = 256;
	protected int height = 64;
	protected int depth = 256;
	private int maximumVolumeSize = 256; // FIXME - Should get this from the library.
	
	protected void OnGuiHeader(bool drawSizeSelector)
	{
		GUIStyle labelWrappingStyle = new GUIStyle(GUI.skin.label);
		labelWrappingStyle.wordWrap = true;
		
		GUIStyle rightAlignmentStyle = new GUIStyle(GUI.skin.textField);
		rightAlignmentStyle.alignment = TextAnchor.MiddleRight;
		
		EditorGUILayout.BeginHorizontal();
			GUILayout.Space(20);
			EditorGUILayout.LabelField("Cubiquity volumes are not Unity3D assets and they do not belong in the 'Assets' folder. " +
				"Please choose or create an empty folder inside the 'Volumes' folder.", labelWrappingStyle);
			GUILayout.Space(20);
		EditorGUILayout.EndHorizontal();
		
		GUILayout.Space(10);
		
		EditorGUILayout.BeginHorizontal();	
			GUILayout.Space(50);
			EditorGUILayout.LabelField("Folder name:", GUILayout.Width(80));
			EditorGUILayout.TextField("", datasetName);
			if(GUILayout.Button("Select folder...", GUILayout.Width(120)))
			{
				string selectedFolderAsString = EditorUtility.SaveFolderPanel("Create or choose and empty folder for the volume data", Cubiquity.pathToData, "");
				
				DirectoryInfo assetDirInfo = new DirectoryInfo(Application.dataPath);
				DirectoryInfo executableDirInfo = assetDirInfo.Parent;
				DirectoryInfo volumeDirInfo = new DirectoryInfo(executableDirInfo.FullName + Path.DirectorySeparatorChar + Cubiquity.pathToData);
			
				Uri volumeUri = new Uri(volumeDirInfo.FullName + Path.DirectorySeparatorChar);
				Uri selectedUri = new Uri(selectedFolderAsString);
				Uri relativeUri = volumeUri.MakeRelativeUri(selectedUri);
			
				datasetName = relativeUri.ToString();
			}
			GUILayout.Space(20);
		EditorGUILayout.EndHorizontal();
		
		GUILayout.Space(10);
		
		if(drawSizeSelector)
		{					
			EditorGUILayout.BeginHorizontal();
				GUILayout.Space(20);
				EditorGUILayout.LabelField("Set the volume dimensions below. Please note that the values cannot exceed 256 in any dimension.", labelWrappingStyle);
				GUILayout.Space(20);
			EditorGUILayout.EndHorizontal();
			
			GUILayout.Space(10);
			
			EditorGUILayout.BeginHorizontal();
				GUILayout.Space(50);
				EditorGUILayout.LabelField("Width:", GUILayout.Width(50));
				width = Math.Min(EditorGUILayout.IntField("", width, GUILayout.Width(40)), maximumVolumeSize);
				GUILayout.Space(20);
				EditorGUILayout.LabelField("Height:", GUILayout.Width(50));
				height = Math.Min(EditorGUILayout.IntField("", height, GUILayout.Width(40)), maximumVolumeSize);
				GUILayout.Space(20);
				EditorGUILayout.LabelField("Depth:", GUILayout.Width(50));
				depth = Math.Min(EditorGUILayout.IntField("", depth, GUILayout.Width(40)), maximumVolumeSize);
				GUILayout.FlexibleSpace();
			EditorGUILayout.EndHorizontal();
		}
	}
	
	protected void OnGuiFooter()
	{
		EditorGUILayout.BeginHorizontal();
			EditorGUILayout.Space();
			if(GUILayout.Button("Create volume", GUILayout.Width(128)))
			{
				OnCreatePressed ();
			}
			GUILayout.Space(50);
			if(GUILayout.Button("Cancel", GUILayout.Width(128)))
			{
				OnCancelPressed ();
			}
			EditorGUILayout.Space();
		EditorGUILayout.EndHorizontal();
	}
	
	abstract public void OnCreatePressed();
	
	void OnCancelPressed()
	{
		Close ();
	}
}