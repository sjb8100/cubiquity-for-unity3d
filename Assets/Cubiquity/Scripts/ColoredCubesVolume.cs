using UnityEngine;
using System;
using System.Collections;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Cubiquity
{
	public struct CubiquityVertex 
	{
		// Disable 'Field ... is never assigned to'
		// warnings as this structure is just for interop
		#pragma warning disable 0649
		public float x;
		public float y;
		public float z;
		public QuantizedColor color;
		#pragma warning restore 0649
	}
	
	[ExecuteInEditMode]
	public class ColoredCubesVolume : MonoBehaviour
	{	
		// The name of the dataset to load from disk.
		[SerializeField]
		private ColoredCubesVolumeData mData = null;
		public ColoredCubesVolumeData data
	    {
	        get { return this.mData; }
			set {this.mData = value; }
	    }
		
		// Determines whether collision data is generated as well as a
		// renderable mesh. This does not apply when in the Unity editor.
		public bool UseCollisionMesh = true;
		
		// This corresponds to the root OctreeNode in Cubiquity.
		private GameObject rootGameObject;
		
		private int maxNodeSyncsPerFrame = 4;
		private int nodeSyncsThisFrame = 0;
		
		public static GameObject CreateGameObject(ColoredCubesVolumeData data)
		{			
			GameObject VoxelTerrainRoot = new GameObject("Colored Cubes Volume");
			VoxelTerrainRoot.AddComponent<ColoredCubesVolume>();
			
			ColoredCubesVolume coloredCubesVolume = VoxelTerrainRoot.GetComponent<ColoredCubesVolume>();
			
			coloredCubesVolume.mData = data;
			
			return VoxelTerrainRoot;
		}
		
		// It seems that we need to implement this function in order to make the volume pickable in the editor.
		// It's actually the gizmo which get's picked which is often bigger than than the volume (unless all
		// voxels are solid). So somtimes the volume will be selected by clicking on apparently empty space.
		// We shold try and fix this by using raycasting to check if a voxel is under the mouse cursor?
		void OnDrawGizmos()
		{
			// Compute the size of the volume.
			int width = (data.enclosingRegion.upperCorner.x - data.enclosingRegion.lowerCorner.x) + 1;
			int height = (data.enclosingRegion.upperCorner.y - data.enclosingRegion.lowerCorner.y) + 1;
			int depth = (data.enclosingRegion.upperCorner.z - data.enclosingRegion.lowerCorner.z) + 1;
			float offsetX = width / 2;
			float offsetY = height / 2;
			float offsetZ = depth / 2;
			
			// The origin is at the centre of a voxel, but we want this box to start at the corner of the voxel.
			Vector3 halfVoxelOffset = new Vector3(0.5f, 0.5f, 0.5f);
			
			// Draw an invisible box surrounding the olume. This is what actually gets picked.
	        Gizmos.color = new Color(1.0f, 0.0f, 0.0f, 0.0f);
			Gizmos.DrawCube (transform.position - halfVoxelOffset + new Vector3(offsetX, offsetY, offsetZ), new Vector3 (width, height, depth));
	    }
		
		public void Synchronize()
		{
			nodeSyncsThisFrame = 0;
			
			if(data.volumeHandle.HasValue)
			{
				CubiquityDLL.UpdateVolume(data.volumeHandle.Value);
				
				if(CubiquityDLL.HasRootOctreeNode(data.volumeHandle.Value) == 1)
				{		
					uint rootNodeHandle = CubiquityDLL.GetRootOctreeNode(data.volumeHandle.Value);
				
					if(rootGameObject == null)
					{					
						rootGameObject = BuildGameObjectFromNodeHandle(rootNodeHandle, null);	
					}
					syncNode(rootNodeHandle, rootGameObject);
				}
			}
		}
		
		void OnEnable()
		{
			Debug.Log ("ColoredCubesVolume.OnEnable()");
		}
		
		// Update is called once per frame
		void Update()
		{
			Synchronize();
		}
		
		public void OnDisable()
		{
			Debug.Log ("ColoredCubesVolume.OnDisable()");
			
			// Game objects in our tree are created with the 'DontSave' flag set, and according to the Unity docs this means
			// we have to destroy them manually. In the case of 'Destroy' the Unity docs explicitally say that it will destroy
			// transform children as well, so I'm assuming DestroyImmediate has the same behaviour.
			DestroyImmediate(rootGameObject);
		}
		
		public void syncNode(uint nodeHandle, GameObject gameObjectToSync)
		{
			if(nodeSyncsThisFrame >= maxNodeSyncsPerFrame)
			{
				return;
			}
			
			uint meshLastUpdated = CubiquityDLL.GetMeshLastUpdated(nodeHandle);		
			OctreeNodeData octreeNodeData = (OctreeNodeData)(gameObjectToSync.GetComponent<OctreeNodeData>());
			
			if(octreeNodeData.meshLastSyncronised < meshLastUpdated)
			{			
				if(CubiquityDLL.NodeHasMesh(nodeHandle) == 1)
				{				
					Mesh renderingMesh;
					Mesh physicsMesh;
					
					BuildMeshFromNodeHandle(nodeHandle, out renderingMesh, out physicsMesh);
			
			        MeshFilter mf = (MeshFilter)gameObjectToSync.GetComponent(typeof(MeshFilter));
			        MeshRenderer mr = (MeshRenderer)gameObjectToSync.GetComponent(typeof(MeshRenderer));
					
					if(mf.sharedMesh != null)
					{
						DestroyImmediate(mf.sharedMesh);
					}
					
			        mf.sharedMesh = renderingMesh;				
					
					mr.material = new Material(Shader.Find("ColoredCubesVolume"));
					
					if(UseCollisionMesh)
					{
						MeshCollider mc = (MeshCollider)gameObjectToSync.GetComponent(typeof(MeshCollider));
						mc.sharedMesh = physicsMesh;
					}
				}
				
				uint currentTime = CubiquityDLL.GetCurrentTime();
				octreeNodeData.meshLastSyncronised = (int)(currentTime);
				
				nodeSyncsThisFrame++;
			}		
			
			//Now syncronise any children
			for(uint z = 0; z < 2; z++)
			{
				for(uint y = 0; y < 2; y++)
				{
					for(uint x = 0; x < 2; x++)
					{
						if(CubiquityDLL.HasChildNode(nodeHandle, x, y, z) == 1)
						{					
						
							uint childNodeHandle = CubiquityDLL.GetChildNode(nodeHandle, x, y, z);					
							
							GameObject childGameObject = octreeNodeData.GetChild(x,y,z);
							
							if(childGameObject == null)
							{							
								childGameObject = BuildGameObjectFromNodeHandle(childNodeHandle, gameObjectToSync);
								
								octreeNodeData.SetChild(x, y, z, childGameObject);
							}
							
							syncNode(childNodeHandle, childGameObject);
						}
					}
				}
			}
		}
		
		GameObject BuildGameObjectFromNodeHandle(uint nodeHandle, GameObject parentGameObject)
		{
			int xPos, yPos, zPos;
			//Debug.Log("Getting position for node handle = " + nodeHandle);
			CubiquityDLL.GetNodePosition(nodeHandle, out xPos, out yPos, out zPos);
			
			StringBuilder name = new StringBuilder("(" + xPos + ", " + yPos + ", " + zPos + ")");
			
			GameObject newGameObject = new GameObject(name.ToString ());
			newGameObject.AddComponent<OctreeNodeData>();
			newGameObject.AddComponent<MeshFilter>();
			newGameObject.AddComponent<MeshRenderer>();
			newGameObject.AddComponent<MeshCollider>();
			
			OctreeNodeData octreeNodeData = newGameObject.GetComponent<OctreeNodeData>();
			octreeNodeData.lowerCorner = new Vector3(xPos, yPos, zPos);
			
			if(parentGameObject)
			{
				newGameObject.transform.parent = parentGameObject.transform;
				
				Vector3 parentLowerCorner = parentGameObject.GetComponent<OctreeNodeData>().lowerCorner;
				newGameObject.transform.localPosition = octreeNodeData.lowerCorner - parentLowerCorner;
			}
			else
			{
				newGameObject.transform.localPosition = octreeNodeData.lowerCorner;
			}
			
			newGameObject.hideFlags = HideFlags.HideAndDontSave;
			
			return newGameObject;
		}
		
		void BuildMeshFromNodeHandle(uint nodeHandle, out Mesh renderingMesh, out Mesh physicsMesh)
		{
			// At some point I should read this: http://forum.unity3d.com/threads/5687-C-plugin-pass-arrays-from-C
			
			// Create rendering and possible collision meshes.
			renderingMesh = new Mesh();		
			physicsMesh = UseCollisionMesh ? new Mesh() : null;
			
			// Get the data from Cubiquity.
			int[] indices = CubiquityDLL.GetIndices(nodeHandle);		
			CubiquityVertex[] cubiquityVertices = CubiquityDLL.GetVertices(nodeHandle);			
			
			// Create the arrays which we'll copy the data to.
	        Vector3[] renderingVertices = new Vector3[cubiquityVertices.Length];	
			Color32[] renderingColors = new Color32[cubiquityVertices.Length];
			Vector3[] physicsVertices = UseCollisionMesh ? new Vector3[cubiquityVertices.Length] : null;
			
			for(int ct = 0; ct < cubiquityVertices.Length; ct++)
			{
				// Get the vertex data from Cubiquity.
				Vector3 position = new Vector3(cubiquityVertices[ct].x, cubiquityVertices[ct].y, cubiquityVertices[ct].z);
				QuantizedColor color = cubiquityVertices[ct].color;
					
				// Copy it to the arrays.
				renderingVertices[ct] = position;
				renderingColors[ct] = (Color32)color;
				
				if(UseCollisionMesh)
				{
					physicsVertices[ct] = position;
				}
			}
			
			// Assign vertex data to the meshes.
			renderingMesh.vertices = renderingVertices; 
			renderingMesh.colors32 = renderingColors;
			renderingMesh.triangles = indices;
			
			// FIXME - Get proper bounds
			renderingMesh.bounds = new Bounds(new Vector3(0.0f, 0.0f, 0.0f), new Vector3(500.0f, 500.0f, 500.0f));
			
			if(UseCollisionMesh)
			{
				physicsMesh.vertices = physicsVertices;
				physicsMesh.triangles = indices;
			}
		}
	}
}
