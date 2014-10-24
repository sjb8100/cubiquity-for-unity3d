﻿using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

using System;
using System.Collections;
using System.Text;

namespace Cubiquity
{
	namespace Impl
	{
		public class OctreeNode : MonoBehaviour
		{
            [System.NonSerialized]
            public uint nodeLastChanged;
			[System.NonSerialized]
			public uint meshLastSyncronised;
            [System.NonSerialized]
            public uint oldestMeshSync = 0;
			[System.NonSerialized]
			public uint lastSyncronisedWithVolumeRenderer;
			[System.NonSerialized]
			public uint lastSyncronisedWithVolumeCollider;
			[System.NonSerialized]
			public Vector3 lowerCorner;
			[System.NonSerialized]
			public GameObject[,,] children;
			
			[System.NonSerialized]
			public uint nodeHandle;

			public static GameObject CreateOctreeNode(uint nodeHandle, GameObject parentGameObject)
			{
                // Get node position from Cubiquity
				int xPos, yPos, zPos;
				CubiquityDLL.GetNodePosition(nodeHandle, out xPos, out yPos, out zPos);
				
                // Build a corresponding game object
				StringBuilder name = new StringBuilder("OctreeNode (" + xPos + ", " + yPos + ", " + zPos + ")");				
				GameObject newGameObject = new GameObject(name.ToString ());
                //newGameObject.hideFlags = HideFlags.HideInHierarchy;
                newGameObject.hideFlags = HideFlags.DontSave;

                // Use parent properties as appropriate
                newGameObject.transform.parent = parentGameObject.transform;
                newGameObject.layer = parentGameObject.layer;

                // It seems that setting the parent does not cause the object to move as Unity adjusts 
                // the child transform to compensate (this can be seen when moving objects between parents 
                // in the hierarchy view). Reset the local transform as shown here: http://goo.gl/k5n7M7
                newGameObject.transform.localRotation = Quaternion.identity;
                newGameObject.transform.localPosition = Vector3.zero;                
                newGameObject.transform.localScale = Vector3.one;

                // Attach an OctreeNode component
                OctreeNode octreeNode = newGameObject.AddComponent<OctreeNode>();
                octreeNode.lowerCorner = new Vector3(xPos, yPos, zPos);
                octreeNode.nodeHandle = nodeHandle;
					
                // Does the parent game object have an octree node attached?
				OctreeNode parentOctreeNode = parentGameObject.GetComponent<OctreeNode>();					
				if(parentOctreeNode)
				{
                    // Cubiquity gives us absolute positions for the Octree nodes, but for a hierarchy of
                    // GameObjects we need relative positions. Obtain these by subtracting parent position.
                    newGameObject.transform.localPosition = octreeNode.lowerCorner - parentOctreeNode.lowerCorner;
				}
				else
				{
                    // If not then the parent must be the Volume GameObject and the one we are creating
                    // must be the root of the Octree. In this case we can use the position directly.
					newGameObject.transform.localPosition = octreeNode.lowerCorner;
				}
				
				return newGameObject;
			}

            public static int syncNodeStructure(int availableNodeSyncs, GameObject nodeGameObject, GameObject voxelTerrainGameObject)
			{
				int nodeSyncsPerformed = 0;				
				if(availableNodeSyncs <= 0)
				{
					return nodeSyncsPerformed;
				}

                OctreeNode octreeNode = nodeGameObject.GetComponent<OctreeNode>();

                uint lastChanged = CubiquityDLL.GetLastChanged(octreeNode.nodeHandle);
                uint meshOrChildMeshLastUpdated = CubiquityDLL.GetMeshOrChildMeshLastUpdated(octreeNode.nodeHandle);

                if ((octreeNode.nodeLastChanged < lastChanged) || (octreeNode.oldestMeshSync < meshOrChildMeshLastUpdated))
                {
                    uint meshLastUpdated = CubiquityDLL.GetMeshLastUpdated(octreeNode.nodeHandle);
                    if (octreeNode.meshLastSyncronised < meshLastUpdated)
                    {
                        if (CubiquityDLL.NodeHasMesh(octreeNode.nodeHandle) == 1)
                        {
                            // Set up the rendering mesh											
                            VolumeRenderer volumeRenderer = voxelTerrainGameObject.GetComponent<VolumeRenderer>();
                            if (volumeRenderer != null)
                            {
                                //Mesh renderingMesh = volumeRenderer.BuildMeshFromNodeHandle(nodeHandle);

                                Mesh renderingMesh = null;
                                if (voxelTerrainGameObject.GetComponent<Volume>().GetType() == typeof(TerrainVolume))
                                {
                                    renderingMesh = MeshConversion.BuildMeshFromNodeHandleForTerrainVolume(octreeNode.nodeHandle, false);
                                }
                                else if (voxelTerrainGameObject.GetComponent<Volume>().GetType() == typeof(ColoredCubesVolume))
                                {
                                    renderingMesh = MeshConversion.BuildMeshFromNodeHandleForColoredCubesVolume(octreeNode.nodeHandle, false);
                                }

                                MeshFilter meshFilter = nodeGameObject.GetOrAddComponent<MeshFilter>() as MeshFilter;
                                MeshRenderer meshRenderer = nodeGameObject.GetOrAddComponent<MeshRenderer>() as MeshRenderer;

                                if (meshFilter.sharedMesh != null)
                                {
                                    DestroyImmediate(meshFilter.sharedMesh);
                                }

                                meshFilter.sharedMesh = renderingMesh;

                                meshRenderer.sharedMaterial = volumeRenderer.material;
                            }

                            // Set up the collision mesh
                            VolumeCollider volumeCollider = voxelTerrainGameObject.GetComponent<VolumeCollider>();
                            if ((volumeCollider != null) && (Application.isPlaying))
                            {
                                Mesh collisionMesh = volumeCollider.BuildMeshFromNodeHandle(octreeNode.nodeHandle);
                                MeshCollider meshCollider = nodeGameObject.GetOrAddComponent<MeshCollider>() as MeshCollider;
                                meshCollider.sharedMesh = collisionMesh;
                            }
                        }
                        // If there is no mesh in Cubiquity then we make sure there isn't on in Unity.
                        else
                        {
                            MeshCollider meshCollider = nodeGameObject.GetComponent<MeshCollider>() as MeshCollider;
                            if (meshCollider)
                            {
                                DestroyImmediate(meshCollider);
                            }

                            MeshRenderer meshRenderer = nodeGameObject.GetComponent<MeshRenderer>() as MeshRenderer;
                            if (meshRenderer)
                            {
                                DestroyImmediate(meshRenderer);
                            }

                            MeshFilter meshFilter = nodeGameObject.GetComponent<MeshFilter>() as MeshFilter;
                            if (meshFilter)
                            {
                                DestroyImmediate(meshFilter);
                            }
                        }

                        octreeNode.meshLastSyncronised = CubiquityDLL.GetCurrentTime(); // Could perhaps just use the meshLastUpdated time here?
                        availableNodeSyncs--;
                        nodeSyncsPerformed++;
                    }

                    uint renderThisNode = 0;
                    renderThisNode = CubiquityDLL.RenderThisNode(octreeNode.nodeHandle);

                    octreeNode.oldestMeshSync = octreeNode.meshLastSyncronised;

                    //Now syncronise any children
                    for (uint z = 0; z < 2; z++)
                    {
                        for (uint y = 0; y < 2; y++)
                        {
                            for (uint x = 0; x < 2; x++)
                            {
                                if (CubiquityDLL.HasChildNode(octreeNode.nodeHandle, x, y, z) == 1 && renderThisNode == 0)
                                {
                                    uint childNodeHandle = CubiquityDLL.GetChildNode(octreeNode.nodeHandle, x, y, z);

                                    if (octreeNode.GetChild(x, y, z) == null)
                                    {
                                        octreeNode.SetChild(x, y, z, OctreeNode.CreateOctreeNode(childNodeHandle, nodeGameObject));
                                    }

                                    //OctreeNode childOctreeNode = octreeNode.GetChild(x, y, z).GetComponent<OctreeNode>();
                                    int syncs = OctreeNode.syncNodeStructure(availableNodeSyncs, octreeNode.GetChild(x, y, z), voxelTerrainGameObject);
                                    availableNodeSyncs -= syncs;
                                    nodeSyncsPerformed += syncs;

                                    octreeNode.oldestMeshSync = Math.Min(octreeNode.oldestMeshSync, octreeNode.GetChild(x, y, z).GetComponent<OctreeNode>().oldestMeshSync);
                                }
                                else
                                {
                                    if (octreeNode.GetChild(x, y, z))
                                    {
                                        Utility.DestroyImmediateWithChildren(octreeNode.GetChild(x, y, z));
                                        octreeNode.SetChild(x, y, z, null);
                                    }
                                }
                            }
                        }
                    }

                    octreeNode.nodeLastChanged = CubiquityDLL.GetCurrentTime();
                }
				
				return nodeSyncsPerformed;
			}

            public void syncNodeProperties(GameObject voxelTerrainGameObject)
            {
                VolumeRenderer vr = voxelTerrainGameObject.GetComponent<VolumeRenderer>();
                MeshRenderer mr = gameObject.GetComponent<MeshRenderer>();
                if (vr != null && mr != null)
                {
                    uint renderThisNode = 0;
                    renderThisNode = CubiquityDLL.RenderThisNode(nodeHandle);

                    mr.enabled = vr.enabled && (renderThisNode != 0);

                    if (lastSyncronisedWithVolumeRenderer < vr.lastModified)
                    {
                        mr.receiveShadows = vr.receiveShadows;
                        mr.castShadows = vr.castShadows;
#if UNITY_EDITOR
                        EditorUtility.SetSelectedWireframeHidden(mr, !vr.showWireframe);
#endif
                        lastSyncronisedWithVolumeRenderer = Clock.timestamp;
                    }
                }

                VolumeCollider vc = voxelTerrainGameObject.GetComponent<VolumeCollider>();
                MeshCollider mc = gameObject.GetComponent<MeshCollider>();
                if (vc != null && mc != null)
                {
                    if (mc.enabled != vc.enabled) // Not sure we really need this check?
                    {
                        mc.enabled = vc.enabled;
                    }

                    if (lastSyncronisedWithVolumeCollider < vc.lastModified)
                    {
                        // Actual syncronization to be filled in in the future when we have something to syncronize.
                        lastSyncronisedWithVolumeCollider = Clock.timestamp;
                    }
                }

                //Now syncronise any children
                for (uint z = 0; z < 2; z++)
                {
                    for (uint y = 0; y < 2; y++)
                    {
                        for (uint x = 0; x < 2; x++)
                        {
                            GameObject childGameObject = GetChild(x, y, z);
                            if(childGameObject != null)
                            {
                                OctreeNode childOctreeNode = childGameObject.GetComponent<OctreeNode>();
                                childOctreeNode.syncNodeProperties(voxelTerrainGameObject);
                            }
                        }
                    }
                }
            }
			
			public GameObject GetChild(uint x, uint y, uint z)
			{
				if(children != null)
				{
					return children[x, y, z];
				}
				else
				{
					return null;
				}
			}
			
			public void SetChild(uint x, uint y, uint z, GameObject gameObject)
			{
				if(children == null)
				{
					children = new GameObject[2, 2, 2];
				}
				
				children[x, y, z] = gameObject;
			}
		}
	}
}
