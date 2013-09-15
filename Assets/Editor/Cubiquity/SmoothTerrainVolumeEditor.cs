﻿using UnityEngine;
using UnityEditor;
using System.Collections;
 
[CustomEditor (typeof(SmoothTerrainVolume))]
public class SmoothTerrainVolumeEditor : Editor
{
	SmoothTerrainVolume smoothTerrainVolume;
	
	private float brushSize = 5.0f;
	private float opacity = 1.0f;
	
	bool sculptPressed = true;
	bool smoothPressed = false;
	bool paintPressed = false;
	bool settingPressed = false;
	
	int selectedTexture = 0;

	public void OnEnable()
	{
	    smoothTerrainVolume = target as SmoothTerrainVolume;
	}
	
	public override void OnInspectorGUI()
	{
		EditorGUILayout.BeginHorizontal();
		if(GUILayout.Toggle(sculptPressed, "Sculpt", EditorStyles.miniButtonLeft))
		{
			sculptPressed = true;
			smoothPressed = false;
			paintPressed = false;
			settingPressed = false;
		}
		if(GUILayout.Toggle(smoothPressed, "Smooth", EditorStyles.miniButtonMid))
		{
			sculptPressed = false;
			smoothPressed = true;
			paintPressed = false;
			settingPressed = false;
		}
		if(GUILayout.Toggle(paintPressed, "Paint", EditorStyles.miniButtonMid))
		{
			sculptPressed = false;
			smoothPressed = false;
			paintPressed = true;
			settingPressed = false;
		}
		if(GUILayout.Toggle(settingPressed, "Settings", EditorStyles.miniButtonRight))
		{
			sculptPressed = false;
			smoothPressed = false;
			paintPressed = false;
			settingPressed = true;
		}
		EditorGUILayout.EndHorizontal();
			
		if(sculptPressed)
		{
			EditorGUILayout.BeginHorizontal();
				EditorGUILayout.LabelField("Brush Size:", GUILayout.Width(80));
				brushSize = GUILayout.HorizontalSlider(brushSize, 0.0f, 10.0f);
			EditorGUILayout.EndHorizontal();
			
			EditorGUILayout.BeginHorizontal();
				EditorGUILayout.LabelField("Opacity:", GUILayout.Width(80));
				opacity = GUILayout.HorizontalSlider(opacity, -2.0f, 2.0f);
			EditorGUILayout.EndHorizontal();
		}
		
		if(smoothPressed)
		{
			EditorGUILayout.BeginHorizontal();
				EditorGUILayout.LabelField("Brush Size:", GUILayout.Width(80));
				brushSize = GUILayout.HorizontalSlider(brushSize, 0.0f, 10.0f);
			EditorGUILayout.EndHorizontal();
			
			EditorGUILayout.BeginHorizontal();
				EditorGUILayout.LabelField("Opacity:", GUILayout.Width(80));
				opacity = GUILayout.HorizontalSlider(opacity, 0.0f, 2.0f);
			EditorGUILayout.EndHorizontal();
		}
		
		if(paintPressed)
		{
			EditorGUILayout.BeginHorizontal();
				EditorGUILayout.LabelField("Brush Size:", GUILayout.Width(80));
				brushSize = GUILayout.HorizontalSlider(brushSize, 0.0f, 10.0f);
			EditorGUILayout.EndHorizontal();
			
			EditorGUILayout.BeginHorizontal();
				EditorGUILayout.LabelField("Opacity:", GUILayout.Width(80));
				opacity = GUILayout.HorizontalSlider(opacity, 0.0f, 2.0f);
			EditorGUILayout.EndHorizontal();
			
			// Don't think the selection grid handles wrapping automatically, so we compute it ourselves.
			int imageThumbnailSize = 80;
			int inspectorWidth = Screen.width;			
			int widthInThumbnails = inspectorWidth / imageThumbnailSize;
			int noOfThumbbails = (int)License.MaxNoOfMaterials;
			int noOfRows = noOfThumbbails / widthInThumbnails;
			if(noOfThumbbails % widthInThumbnails != 0)
			{
				noOfRows++;
			}
			
			//No draw the texture selection grid
			Texture[] images = smoothTerrainVolume.diffuseMaps;
			selectedTexture = GUILayout.SelectionGrid (selectedTexture, images, widthInThumbnails, GUILayout.Height(imageThumbnailSize * noOfRows));
			
			for(int ct = 0; ct < License.MaxNoOfMaterials; ct++)
			{
				smoothTerrainVolume.diffuseMaps[ct] = EditorGUILayout.ObjectField(smoothTerrainVolume.diffuseMaps[ct],typeof(Texture),false, GUILayout.Width(imageThumbnailSize), GUILayout.Height(imageThumbnailSize)) as Texture2D;
			}
		}
	}
	
	public void OnSceneGUI()
	{
		//Debug.Log ("ColoredCubesVolumeEditor.OnSceneGUI()");
		Event e = Event.current;
		
		Ray ray = Camera.current.ScreenPointToRay(new Vector3(e.mousePosition.x, -e.mousePosition.y + Camera.current.pixelHeight));
		Vector3 dir = ray.direction * 1000.0f; //The maximum distance our ray will be cast.
		
		if(((e.type == EventType.MouseDown) || (e.type == EventType.MouseDrag)) && (e.button == 0))
		{
			// Perform the raycasting. If there's a hit the position will be stored in these ints.
			float resultX, resultY, resultZ;

			bool hit = Cubiquity.PickTerrainSurface(smoothTerrainVolume, ray.origin.x, ray.origin.y, ray.origin.z, dir.x, dir.y, dir.z, out resultX, out resultY, out resultZ);
			if(hit)
			{
				if(sculptPressed)
				{
					Cubiquity.SculptSmoothTerrainVolume(smoothTerrainVolume, resultX, resultY, resultZ, brushSize, opacity);
				}
				else if(smoothPressed)
				{
					Cubiquity.BlurSmoothTerrainVolume(smoothTerrainVolume, resultX, resultY, resultZ, brushSize, opacity);
				}
				else if(paintPressed)
				{
					Cubiquity.PaintSmoothTerrainVolume(smoothTerrainVolume, resultX, resultY, resultZ, brushSize, (uint)selectedTexture, opacity);
				}
			}
			
			//Selection.activeGameObject = coloredCubesVolume.gameObject;
		}
		else if ( e.type == EventType.Layout )
	    {
	       // See: http://answers.unity3d.com/questions/303248/how-to-paint-objects-in-the-editor.html
	       HandleUtility.AddDefaultControl( GUIUtility.GetControlID( GetHashCode(), FocusType.Passive ) );
	    }
		
		smoothTerrainVolume.Synchronize();
	}
}