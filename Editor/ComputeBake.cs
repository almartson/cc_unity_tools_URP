/* 
 * Copyright (C) 2021 Victor Soupday
 * This file is part of CC3_Unity_Tools <https://github.com/soupday/cc3_unity_tools>
 * 
 * CC3_Unity_Tools is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 * 
 * CC3_Unity_Tools is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with CC3_Unity_Tools.  If not, see <https://www.gnu.org/licenses/>.
 */

using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

namespace Reallusion.Import
{
    public class ComputeBake
    {
        private readonly GameObject fbx;
        private GameObject clone;
        private CharacterInfo characterInfo;
        private readonly string characterName;
        private readonly string fbxPath;
        private readonly string fbxFolder;
        private readonly string bakeFolder;
        private readonly string characterFolder;
        private readonly string texturesFolder;
        private readonly string materialsFolder;
        private readonly string fbmFolder;
        private readonly string texFolder;
        private readonly List<string> sourceTextureFolders;        
        private readonly ModelImporter importer;
        private readonly List<string> importAssets;

        public const int MAX_SIZE = 4096;
        public const string COMPUTE_SHADER = "RLBakeShader";
        public const string BAKE_FOLDER = "Baked";
        public const string TEXTURES_FOLDER = "Textures";
        public const string MATERIALS_FOLDER = "Materials";                

        private Texture2D maskTex = null;

        public ComputeBake(UnityEngine.Object character, CharacterInfo info)
        {
            fbx = (GameObject)character;
            fbxPath = AssetDatabase.GetAssetPath(fbx);
            characterInfo = info;
            characterName = Path.GetFileNameWithoutExtension(fbxPath);
            fbxFolder = Path.GetDirectoryName(fbxPath);
            bakeFolder = Util.CreateFolder(fbxFolder, BAKE_FOLDER);
            characterFolder = Util.CreateFolder(bakeFolder, characterName);
            texturesFolder = Util.CreateFolder(characterFolder, TEXTURES_FOLDER);
            materialsFolder = Util.CreateFolder(characterFolder, MATERIALS_FOLDER);

            fbmFolder = Path.Combine(fbxFolder, characterName + ".fbm");
            texFolder = Path.Combine(fbxFolder, "textures", characterName);
            sourceTextureFolders = new List<string>() { fbmFolder, texFolder };

            importer = (ModelImporter)AssetImporter.GetAtPath(fbxPath);

            importAssets = new List<string>();
        }

        private static Vector2Int GetMaxSize(Texture2D a)
        {
            Vector2Int max = new Vector2Int(a.width, a.height);
            if (max.x > MAX_SIZE) max.x = MAX_SIZE;
            if (max.y > MAX_SIZE) max.y = MAX_SIZE;
            return max;
        }

        private static Vector2Int GetMaxSize(Texture2D a, Texture2D b)
        {
            Vector2Int max = new Vector2Int(a.width, a.height);
            if (b.width > max.x) max.x = b.width;
            if (b.height > max.y) max.y = b.height;
            if (max.x > MAX_SIZE) max.x = MAX_SIZE;
            if (max.y > MAX_SIZE) max.y = MAX_SIZE;
            return max;
        }

        private static Vector2Int GetMaxSize(Texture2D a, Texture2D b, Texture2D c)
        {
            Vector2Int max = new Vector2Int(a.width, a.height);
            if (b.width > max.x) max.x = b.width;
            if (b.height > max.y) max.y = b.height;
            if (c.width > max.x) max.x = c.width;
            if (c.height > max.y) max.y = c.height;
            if (max.x > MAX_SIZE) max.x = MAX_SIZE;
            if (max.y > MAX_SIZE) max.y = MAX_SIZE;
            return max;
        }

        private static Vector2Int GetMaxSize(Texture2D a, Texture2D b, Texture2D c, Texture2D d)
        {
            Vector2Int max = new Vector2Int(a.width, a.height);
            if (b.width > max.x) max.x = b.width;
            if (b.height > max.y) max.y = b.height;
            if (c.width > max.x) max.x = c.width;
            if (c.height > max.y) max.y = c.height;
            if (d.width > max.x) max.x = d.width;
            if (d.height > max.y) max.y = d.height;
            if (max.x > MAX_SIZE) max.x = MAX_SIZE;
            if (max.y > MAX_SIZE) max.y = MAX_SIZE;
            return max;
        }        

        private Texture2D GetMaterialTexture(Material mat, string shaderRef, bool isNormal = false)
        {
            Texture2D tex = (Texture2D)mat.GetTexture(shaderRef);
            string assetPath = AssetDatabase.GetAssetPath(tex);
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(assetPath);

            if (importer)
            {
                // TODO should the character importer set these as the defaults?
                // Turn off texture compression and unlock max size to 4k, for the best possible quality bake:
                if (importer.textureCompression != TextureImporterCompression.Uncompressed ||
                    importer.compressionQuality != 0 ||
                    importer.maxTextureSize < 4096 ||
                    (isNormal && importer.textureType != TextureImporterType.NormalMap))
                {
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    importer.compressionQuality = 0;
                    importer.maxTextureSize = 4096;
                    if (isNormal) importer.textureType = TextureImporterType.NormalMap;
                    else importer.textureType = TextureImporterType.Default;
                    importer.SaveAndReimport();
                }
            }

            return tex;
        }

        private Texture2D CheckHDRP(Texture2D tex)
        {
            if (tex)
                return tex;

            if (!maskTex)
            {
                maskTex = new Texture2D(8, 8, TextureFormat.ARGB32, false, true);
                Color[] pixels = maskTex.GetPixels();
                for (int i = 0; i < pixels.Length; i++)
                {
                    pixels[i] = new Color(0f, 1f, 1f, 0.5f);
                }
                maskTex.SetPixels(pixels);
            }

            return maskTex;
        }

        /// <summary>
        /// Checks that the texture exists, if not returns a blank white texture.
        /// </summary>
        private Texture2D CheckDiffuse(Texture2D tex)
        {
            if (tex) return tex;
            return Texture2D.whiteTexture;
        }

        /// <summary>
        /// Checks that the texture exists, if not returns a blank normal texture.
        /// </summary>
        private Texture2D CheckNormal(Texture2D tex)
        {
            if (tex) return tex;
            return Texture2D.normalTexture;
        }

        /// <summary>
        /// Checks that the texture exists, if not returns a blank white texture.
        /// </summary>
        private Texture2D CheckMask(Texture2D tex)
        {
            if (tex) return tex;
            return Texture2D.whiteTexture;
        }

        /// <summary>
        /// Checks that the texture exists, if not returns a blank black texture.
        /// </summary>
        private Texture2D CheckBlank(Texture2D tex)
        {
            if (tex) return tex;
            return Texture2D.blackTexture;
        }

        /// <summary>
        /// Checks that the texture exists, if not returns a blank gray texture.
        /// </summary>
        private Texture2D CheckOverlay(Texture2D tex)
        {
            if (tex) return tex;
            return Texture2D.linearGrayTexture;
        }

        public void BakeHQ()
        {
            if (Util.IsCC3Character(fbx))
            {
                CopyToClone();

                int childCount = clone.transform.childCount;
                for (int i = 0; i < childCount; i++)
                {
                    GameObject child = clone.transform.GetChild(i).gameObject;
                    Renderer renderer = child.GetComponent<Renderer>();
                    if (renderer)
                    {
                        foreach (Material sharedMat in renderer.sharedMaterials)
                        {
                            // in case any of the materials have been renamed after a previous import, get the source name.
                            string sourceName = Util.GetSourceMaterialName(fbxPath, sharedMat);                            
                            string shaderName = Util.GetShaderName(sharedMat);
                            Material bakedMaterial = null;

                            switch (shaderName)
                            {
                                case Pipeline.SHADER_HQ_SKIN:
                                    bakedMaterial = BakeSkinMaterial(sharedMat, sourceName);
                                    break;

                                case Pipeline.SHADER_HQ_TEETH:
                                    bakedMaterial = BakeTeethMaterial(sharedMat, sourceName);
                                    break;

                                case Pipeline.SHADER_HQ_TONGUE:
                                    bakedMaterial = BakeTongueMaterial(sharedMat, sourceName);
                                    break;

                                case Pipeline.SHADER_HQ_HAIR:
                                    bakedMaterial = BakeHairMaterial(sharedMat, sourceName);
                                    break;

                                case Pipeline.SHADER_HQ_EYE:
                                    bakedMaterial = BakeEyeMaterial(sharedMat, sourceName);
                                    break;

                                case Pipeline.SHADER_HQ_EYE_OCCLUSION:
                                    bakedMaterial = BakeEyeOcclusionMaterial(sharedMat, sourceName);
                                    break;
                            }

                            if (bakedMaterial)
                            {
                                ReplaceMaterial(sharedMat, bakedMaterial);
                            }
                        }
                    }
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                SaveAsPrefab();

                //System.Media.SystemSounds.Asterisk.Play();
            }
        }        

        private void ReplaceMaterial(Material from, Material to)
        {
            int childCount = clone.transform.childCount;
            for (int i = 0; i < childCount; i++)
            {
                GameObject child = clone.transform.GetChild(i).gameObject;
                Renderer renderer = child.GetComponent<Renderer>();
                if (renderer)
                {
                    for (int j = 0; j < renderer.sharedMaterials.Length; j++)
                    {
                        if (renderer.sharedMaterials[j] == from)
                        {
                            Material[] copy = (Material[])renderer.sharedMaterials.Clone();
                            copy[j] = to;
                            renderer.sharedMaterials = copy;
                        }
                    }
                }
            }
        }

        public void CopyToClone()
        {
            clone = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
        }

        public void SaveAsPrefab()
        {
            string prefabFolder = Util.CreateFolder(fbxFolder, Importer.PREFABS_FOLDER);
            string namedPrefabFolder = Util.CreateFolder(prefabFolder, characterName);
            string prefabPath = Path.Combine(namedPrefabFolder, characterName + "_Baked.prefab");
            GameObject variant = PrefabUtility.SaveAsPrefabAsset(clone, prefabPath);
            Selection.activeObject = variant;
            GameObject.DestroyImmediate(clone);
        }

        private Material CreateBakedMaterial(Texture2D baseMap, Texture2D maskMap, Texture2D normalMap,
            Texture2D detailMap, Texture2D subsurfaceMap, Texture2D thicknessMap, float tiling,
            string sourceName, Material templateMaterial)
        {
            Material bakedMaterial = Util.FindMaterial(sourceName, new string[] { materialsFolder });            
            Shader shader = Pipeline.GetDefaultShader();

            if (!bakedMaterial)
            {
                // create the remapped material and save it as an asset.
                string matPath = AssetDatabase.GenerateUniqueAssetPath(
                        Path.Combine(materialsFolder, sourceName + ".mat")
                    );

                bakedMaterial = new Material(shader);

                // save the material to the asset database.
                AssetDatabase.CreateAsset(bakedMaterial, matPath);
            }

            // copy the template material properties to the remapped material.
            if (templateMaterial)
            {
                if (templateMaterial.shader && templateMaterial.shader != bakedMaterial.shader)
                    bakedMaterial.shader = templateMaterial.shader;
                bakedMaterial.CopyPropertiesFromMaterial(templateMaterial);
            }
            else
            {
                // if the material shader doesn't match, update the shader.            
                if (bakedMaterial.shader != shader)
                    bakedMaterial.shader = shader;
            }

            // apply the textures...
            bakedMaterial.SetTexture("_BaseColorMap", baseMap);
            bakedMaterial.SetTexture("_MaskMap", maskMap);
            bakedMaterial.SetTexture("_NormalMap", normalMap);
            bakedMaterial.SetTexture("_DetailMap", detailMap);
            if (detailMap)
                bakedMaterial.SetTextureScale("_DetailMap", new Vector2(tiling, tiling));
            bakedMaterial.SetTexture("_SubsurfaceMaskMap", subsurfaceMap);
            bakedMaterial.SetTexture("_ThicknessMap", thicknessMap);

            // add the path of the remapped material for later re-import.
            string remapPath = AssetDatabase.GetAssetPath(bakedMaterial);
            if (remapPath == fbxPath) Debug.LogError("remapPath: " + remapPath + " is fbxPath!");
            if (remapPath != fbxPath && AssetDatabase.WriteImportSettingsIfDirty(remapPath))
                importAssets.Add(AssetDatabase.GetAssetPath(bakedMaterial));

            return bakedMaterial;
        }

        private Material BakeSkinMaterial(Material mat, string sourceName)
        {
            Texture2D diffuse = GetMaterialTexture(mat, "_DiffuseMap");
            Texture2D mask = GetMaterialTexture(mat, "_MaskMap");
            Texture2D subsurface = GetMaterialTexture(mat, "_SSSMap");
            Texture2D thickness = GetMaterialTexture(mat, "_ThicknessMap");
            Texture2D normal = GetMaterialTexture(mat, "_NormalMap", true);
            Texture2D microNormal = GetMaterialTexture(mat, "_MicroNormalMap", true);
            Texture2D colorBlend = GetMaterialTexture(mat, "_ColorBlendMap");
            Texture2D cavityAO = GetMaterialTexture(mat, "_MNAOMap");
            Texture2D normalBlend = GetMaterialTexture(mat, "_NormalBlendMap", true);
            Texture2D RGBAMask = GetMaterialTexture(mat, "_RGBAMask");
            Texture2D CFULCMask = GetMaterialTexture(mat, "_CFULCMask");
            Texture2D EarNeckMask = GetMaterialTexture(mat, "_EarNeckMask");            
            float microNormalStrength = mat.GetFloat("_MicroNormalStrength");
            float microNormalTiling = mat.GetFloat("_MicroNormalTiling");
            float aoStrength = mat.GetFloat("_AOStrength");
            float smoothnessMin = mat.GetFloat("_SmoothnessMin");
            float smoothnessMax = mat.GetFloat("_SmoothnessMax");
            float smoothnessPower = mat.GetFloat("_SmoothnessPower");
            float subsurfaceScale = mat.GetFloat("_SubsurfaceScale");
            float thicknessScale = mat.GetFloat("_ThicknessScale");
            float colorBlendStrength = mat.GetFloat("_ColorBlendStrength");
            float normalBlendStrength = mat.GetFloat("_NormalBlendStrength");
            float mouthAOPower = mat.GetFloat("_MouthCavityAO");
            float nostrilAOPower = mat.GetFloat("_NostrilCavityAO");
            float lipsAOPower = mat.GetFloat("_LipsCavityAO");
            float microSmoothnessMod = mat.GetFloat("_MicroSmoothnessMod");
            float rMSM = mat.GetFloat("_RSmoothnessMod");
            float gMSM = mat.GetFloat("_GSmoothnessMod");
            float bMSM = mat.GetFloat("_BSmoothnessMod");
            float aMSM = mat.GetFloat("_ASmoothnessMod");
            float earMSM = mat.GetFloat("_EarSmoothnessMod");
            float neckMSM = mat.GetFloat("_NeckSmoothnessMod");
            float cheekMSM = mat.GetFloat("_CheekSmoothnessMod");
            float foreheadMSM = mat.GetFloat("_ForeheadSmoothnessMod");
            float upperLipMSM = mat.GetFloat("_UpperLipSmoothnessMod");
            float chinMSM = mat.GetFloat("_ChinSmoothnessMod");
            float unmaskedMSM = mat.GetFloat("_UnmaskedSmoothnessMod");            
            float rSS = mat.GetFloat("_RScatterScale");
            float gSS = mat.GetFloat("_GScatterScale");
            float bSS = mat.GetFloat("_BScatterScale");
            float aSS = mat.GetFloat("_AScatterScale");
            float earSS = mat.GetFloat("_EarScatterScale");
            float neckSS = mat.GetFloat("_NeckScatterScale");
            float cheekSS = mat.GetFloat("_CheekScatterScale");
            float foreheadSS = mat.GetFloat("_ForeheadScatterScale");
            float upperLipSS = mat.GetFloat("_UpperLipScatterScale");
            float chinSS = mat.GetFloat("_ChinScatterScale");
            float unmaskedSS = mat.GetFloat("_UnmaskedScatterScale");

            bool isHead = mat.GetFloat("BOOLEAN_IS_HEAD") > 0f;

            Texture2D bakedBaseMap = diffuse;
            Texture2D bakedMaskMap = mask;
            Texture2D bakedNormalMap = normal;
            Texture2D bakedDetailMap = null;
            Texture2D bakedSubsurfaceMap = subsurface;
            Texture2D bakedThicknessMap = thickness;

            if (isHead)
            {
                if (diffuse && colorBlend && cavityAO)
                    bakedBaseMap = BakeHeadDiffuseMap(diffuse, colorBlend, cavityAO, 
                        colorBlendStrength, mouthAOPower, nostrilAOPower, lipsAOPower,                        
                        sourceName + "_BaseMap");

                if (normal && normalBlend)
                    bakedNormalMap = BakeBlendNormalMap(normal, normalBlend,
                        normalBlendStrength,
                        sourceName + "_Normal");

                if (mask && cavityAO && RGBAMask && CFULCMask && EarNeckMask)
                    bakedMaskMap = BakeHeadMaskMap(mask, cavityAO, RGBAMask, CFULCMask, EarNeckMask,
                        aoStrength, smoothnessMin, smoothnessMax, smoothnessPower, microNormalStrength,
                        mouthAOPower, nostrilAOPower, lipsAOPower, microSmoothnessMod,
                        rMSM, gMSM, bMSM, aMSM, earMSM, neckMSM, cheekMSM, foreheadMSM, upperLipMSM, chinMSM, unmaskedMSM,                        
                        sourceName + "_Mask");

                else if (mask)
                    bakedMaskMap = BakeMaskMap(mask,
                        aoStrength, smoothnessMin, smoothnessMax, smoothnessPower, microNormalStrength,
                        sourceName + "_Mask");

                if (subsurface && RGBAMask && CFULCMask && EarNeckMask)
                    bakedSubsurfaceMap = BakeHeadSubsurfaceMap(subsurface, RGBAMask, CFULCMask, EarNeckMask,
                        subsurfaceScale,                         
                        rSS, gSS, bSS, aSS, earSS, neckSS, cheekSS, foreheadSS, upperLipSS, chinSS, unmaskedSS,
                        sourceName + "_SSSMap");

                else if (subsurface)
                    bakedSubsurfaceMap = BakeSubsurfaceMap(subsurface,
                        subsurfaceScale,
                        sourceName + "_SSSMap");

            }
            else
            {
                if (mask && RGBAMask)
                    bakedMaskMap = BakeSkinMaskMap(mask, RGBAMask,
                        aoStrength, smoothnessMin, smoothnessMax, smoothnessPower, microNormalStrength, microSmoothnessMod,
                        rMSM, gMSM, bMSM, aMSM, unmaskedMSM,                        
                        sourceName + "_Mask");

                else if (mask)
                    bakedMaskMap = BakeMaskMap(mask,
                        aoStrength, smoothnessMin, smoothnessMax, smoothnessPower, microNormalStrength,
                        sourceName + "_Mask");

                if (subsurface && RGBAMask)
                    bakedSubsurfaceMap = BakeSkinSubsurfaceMap(subsurface, RGBAMask,
                        subsurfaceScale,                        
                        rSS, gSS, bSS, aSS, unmaskedSS,
                        sourceName + "_SSSMap");

                else if (subsurface)
                    bakedSubsurfaceMap = BakeSubsurfaceMap(subsurface,
                        subsurfaceScale,
                        sourceName + "_SSSMap");
            }

            if (microNormal)
                bakedDetailMap = BakeDetailMap(microNormal,
                    sourceName + "_Detail");            

            if (thickness)
                bakedThicknessMap = BakeThicknessMap(thickness,
                    thicknessScale,
                    sourceName + "_Thickness");

            Material result = CreateBakedMaterial(bakedBaseMap, bakedMaskMap, bakedNormalMap,
                bakedDetailMap, bakedSubsurfaceMap, bakedThicknessMap,
                microNormalTiling,
                sourceName, 
                Pipeline.GetTemplateMaterial(MaterialType.Skin, 
                            MaterialQuality.Baked, characterInfo));            

            return result;
        }

        private Material BakeTeethMaterial(Material mat, string sourceName)
        {
            Texture2D diffuse = GetMaterialTexture(mat, "_DiffuseMap");
            Texture2D mask = GetMaterialTexture(mat, "_MaskMap");
            Texture2D normal = GetMaterialTexture(mat, "_NormalMap", true);
            Texture2D microNormal = GetMaterialTexture(mat, "_MicroNormalMap", true);
            Texture2D gumsMask = GetMaterialTexture(mat, "_GumsMaskMap");
            Texture2D gradientAO = GetMaterialTexture(mat, "_GradientAOMap");
            float microNormalStrength = mat.GetFloat("_MicroNormalStrength");
            float microNormalTiling = mat.GetFloat("_MicroNormalTiling");
            float aoStrength = mat.GetFloat("_AOStrength");
            float smoothnessPower = mat.GetFloat("_SmoothnessPower");
            float smoothnessMin = mat.GetFloat("_SmoothnessMin");
            float smoothnessMax = mat.GetFloat("_SmoothnessMax");
            float gumsSaturation = mat.GetFloat("_GumsSaturation");
            float gumsBrightness = mat.GetFloat("_GumsBrightness");
            float teethSaturation = mat.GetFloat("_TeethSaturation");
            float teethBrightness = mat.GetFloat("_TeethBrightness");
            float frontAO = mat.GetFloat("_FrontAO");
            float rearAO = mat.GetFloat("_RearAO");
            float teethSSS = mat.GetFloat("_TeethSSS");
            float gumsSSS = mat.GetFloat("_GumsSSS");
            float teethThickness = mat.GetFloat("_TeethThickness");
            float gumsThickness = mat.GetFloat("_GumsThickness");
            float isUpperTeeth = mat.GetFloat("_IsUpperTeeth");

            Texture2D bakedBaseMap = diffuse;
            Texture2D bakedMaskMap = mask;
            Texture2D bakedNormalMap = normal;
            Texture2D bakedDetailMap = null;
            Texture2D bakedSubsurfaceMap = null;
            Texture2D bakedThicknessMap = null;

            if (diffuse && gumsMask && gradientAO)
                bakedBaseMap = BakeTeethDiffuseMap(diffuse, gumsMask, gradientAO,
                    isUpperTeeth, frontAO, rearAO, gumsSaturation, gumsBrightness, teethSaturation, teethBrightness,
                    sourceName + "_BaseMap");

            if (mask && gradientAO)
                bakedMaskMap = BakeTeethMaskMap(mask, gradientAO,
                    isUpperTeeth, aoStrength, smoothnessMin, smoothnessMax, smoothnessPower,
                    frontAO, rearAO, microNormalStrength,
                    sourceName + "_Mask");

            else if (mask)
                bakedMaskMap = BakeMaskMap(mask,
                    aoStrength, smoothnessMin, smoothnessMax, smoothnessPower, microNormalStrength,
                    sourceName + "_Mask");

            if (microNormal)
                bakedDetailMap = BakeDetailMap(microNormal,
                    sourceName + "_Detail");

            if (gumsMask && gradientAO)
                bakedSubsurfaceMap = BakeTeethSubsurfaceMap(gumsMask, gradientAO,
                    isUpperTeeth, rearAO, frontAO, gumsSSS, teethSSS,
                    sourceName + "_SSSMap");

            if (gumsMask)
                bakedThicknessMap = BakeTeethThicknessMap(gumsMask,
                    gumsThickness, teethThickness,
                    sourceName + "_Thickness");

            return CreateBakedMaterial(bakedBaseMap, bakedMaskMap, bakedNormalMap,
                bakedDetailMap, bakedSubsurfaceMap, bakedThicknessMap,
                microNormalTiling,
                sourceName, 
                Pipeline.GetTemplateMaterial(MaterialType.Teeth, 
                            MaterialQuality.Baked, characterInfo));
        }

        private Material BakeTongueMaterial(Material mat, string sourceName)
        {
            Texture2D diffuse = GetMaterialTexture(mat, "_DiffuseMap");
            Texture2D mask = GetMaterialTexture(mat, "_MaskMap");
            Texture2D normal = GetMaterialTexture(mat, "_NormalMap", true);
            Texture2D microNormal = GetMaterialTexture(mat, "_MicroNormalMap", true);
            Texture2D gradientAO = GetMaterialTexture(mat, "_GradientAOMap");
            float microNormalStrength = mat.GetFloat("_MicroNormalStrength");
            float microNormalTiling = mat.GetFloat("_MicroNormalTiling");
            float aoStrength = mat.GetFloat("_AOStrength");
            float smoothnessPower = mat.GetFloat("_SmoothnessPower");
            float smoothnessMin = mat.GetFloat("_SmoothnessMin");
            float smoothnessMax = mat.GetFloat("_SmoothnessMax");
            float tongueSaturation = mat.GetFloat("_TongueSaturation");
            float tongueBrightness = mat.GetFloat("_TongueBrightness");
            float frontAO = mat.GetFloat("_FrontAO");
            float rearAO = mat.GetFloat("_RearAO");
            float tongueSSS = mat.GetFloat("_TongueSSS");
            float tongueThickness = mat.GetFloat("_TongueThickness");

            Texture2D bakedBaseMap = diffuse;
            Texture2D bakedMaskMap = mask;
            Texture2D bakedNormalMap = normal;
            Texture2D bakedDetailMap = null;
            Texture2D bakedSubsurfaceMap = null;
            Texture2D bakedThicknessMap = null;

            if (diffuse && gradientAO)
                bakedBaseMap = BakeTongueDiffuseMap(diffuse, gradientAO,
                    frontAO, rearAO, tongueSaturation, tongueBrightness,
                    sourceName + "_BaseMap");

            if (mask && gradientAO)
                bakedMaskMap = BakeTongueMaskMap(mask, gradientAO,
                    aoStrength, smoothnessMin, smoothnessMax, smoothnessPower,
                    frontAO, rearAO, microNormalStrength,
                    sourceName + "_Mask");

            else if (mask)
                bakedMaskMap = BakeMaskMap(mask,
                    aoStrength, smoothnessMin, smoothnessMax, smoothnessPower, microNormalStrength,
                    sourceName + "_Mask");

            if (microNormal)
                bakedDetailMap = BakeDetailMap(microNormal,
                    sourceName + "_Detail");

            if (gradientAO)
                bakedSubsurfaceMap = BakeTongueSubsurfaceMap(gradientAO,
                    rearAO, frontAO, tongueSSS,
                    sourceName + "_SSSMap");

            Material result = CreateBakedMaterial(bakedBaseMap, bakedMaskMap, bakedNormalMap,
                bakedDetailMap, bakedSubsurfaceMap, bakedThicknessMap,
                microNormalTiling,
                sourceName, 
                Pipeline.GetTemplateMaterial(MaterialType.Tongue, 
                            MaterialQuality.Baked, characterInfo));

            result.SetFloat("_Thickness", tongueThickness);
            return result;
        }

        private Material BakeEyeMaterial(Material mat, string sourceName)
        {
            Texture2D sclera = GetMaterialTexture(mat, "_ScleraDiffuseMap");
            Texture2D cornea = GetMaterialTexture(mat, "_CorneaDiffuseMap");
            Texture2D blend = GetMaterialTexture(mat, "_ColorBlendMap");
            Texture2D mask = GetMaterialTexture(mat, "_MaskMap");
            Texture2D microNormal = GetMaterialTexture(mat, "_ScleraNormalMap", true);
            float microNormalStrength = mat.GetFloat("_ScleraNormalStrength");
            float microNormalTiling = mat.GetFloat("_ScleraNormalTiling");
            float aoStrength = mat.GetFloat("_AOStrength");
            float colorBlendStrength = mat.GetFloat("_ColorBlendStrength");
            float scleraSmoothness = mat.GetFloat("_ScleraSmoothness");
            float irisSmoothness = mat.GetFloat("_IrisSmoothness");
            float corneaSmoothness = mat.GetFloat("_CorneaSmoothness");
            float irisHue = mat.GetFloat("_IrisHue");
            float irisSaturation = mat.GetFloat("_IrisSaturation");
            float irisBrightness = mat.GetFloat("_IrisBrightness");
            float scleraHue = mat.GetFloat("_ScleraHue");
            float scleraSaturation = mat.GetFloat("_ScleraSaturation");
            float scleraBrightness = mat.GetFloat("_ScleraBrightness");
            float refractionThickness = mat.GetFloat("_RefractionThickness");
            float shadowRadius = mat.GetFloat("_ShadowRadius");
            float shadowHardness = mat.GetFloat("_ShadowHardness");
            float irisScale = mat.GetFloat("_IrisScale");
            float scleraScale = mat.GetFloat("_ScleraScale");
            float limbusDarkRadius = mat.GetFloat("_LimbusDarkRadius");
            float limbusDarkWidth = mat.GetFloat("_LimbusDarkWidth");
            float irisRadius = mat.GetFloat("_IrisRadius");
            float limbusWidth = mat.GetFloat("_LimbusWidth");
            float ior = mat.GetFloat("_IOR");
            float depthRadius = mat.GetFloat("_DepthRadius");
            float irisDepth = mat.GetFloat("_IrisDepth");
            float pupilScale = mat.GetFloat("_PupilScale");
            Color cornerShadowColor = mat.GetColor("_CornerShadowColor");
            Color limbusColor = mat.GetColor("_LimbusColor");
            bool isCornea = mat.GetFloat("BOOLEAN_ISCORNEA") > 0f;
            bool isLeftEye = mat.GetFloat("_IsLeftEye") > 0f;

            Texture2D bakedBaseMap = cornea;
            Texture2D bakedMaskMap = mask;
            Texture2D bakedNormalMap = null;
            Texture2D bakedDetailMap = null;
            Texture2D bakedSubsurfaceMap = null;
            Texture2D bakedThicknessMap = null;

            if (isCornea)
            {
                if (sclera && blend)
                    bakedBaseMap = BakeCorneaDiffuseMap(sclera, blend,
                        scleraScale, scleraHue, scleraSaturation, scleraBrightness,
                        irisScale, irisRadius, limbusWidth,
                        shadowRadius, shadowHardness, cornerShadowColor,
                        colorBlendStrength,
                        sourceName + "_BaseMap");

                if (mask)
                    bakedMaskMap = BakeCorneaMaskMap(mask, aoStrength, corneaSmoothness, 
                        scleraSmoothness, irisScale,
                        irisRadius, limbusWidth, microNormalStrength,
                        sourceName + "_Mask");

                if (microNormal)
                    bakedDetailMap = BakeDetailMap(microNormal,
                        sourceName + "_Detail");

                // RGB textures cannot store low enough values for the refraction thickness
                // so normalize it and use the thickness remap in the HDRP/Lit shader.
                bakedThicknessMap = BakeCorneaThicknessMap(irisScale, limbusDarkRadius,
                    limbusDarkWidth, refractionThickness / 0.025f,
                    sourceName + "_Thickness");
            }
            else
            {
                if (cornea && blend)
                    bakedBaseMap = BakeEyeDiffuseMap(cornea, blend,
                        irisScale, irisHue, irisSaturation, irisBrightness,
                        limbusDarkRadius, limbusDarkWidth, limbusColor, colorBlendStrength,
                        sourceName + "_BaseMap");

                if (mask)
                    bakedMaskMap = BakeEyeMaskMap(mask, aoStrength, irisSmoothness, 
                        scleraSmoothness, irisScale,
                        limbusDarkRadius, limbusDarkWidth, irisRadius, depthRadius,
                        sourceName + "_Mask");
            }

            // the HDRP/Lit shader seems to use x10 thickness values...
            Material result = CreateBakedMaterial(bakedBaseMap, bakedMaskMap, bakedNormalMap,
                bakedDetailMap, bakedSubsurfaceMap, bakedThicknessMap,
                microNormalTiling,
                sourceName, isCornea ? Pipeline.GetTemplateMaterial(MaterialType.Cornea, 
                                            MaterialQuality.Baked, characterInfo) 
                                     : Pipeline.GetTemplateMaterial(MaterialType.Eye, 
                                            MaterialQuality.Baked, characterInfo));

            if (isCornea)
            {
                result.SetFloat("_Ior", ior);
                result.SetFloat("_Thickness", refractionThickness / 10f);
                Color thicknessRemap;
                thicknessRemap.r = 0f;
                thicknessRemap.g = 0.025f;
                thicknessRemap.b = 0f;
                thicknessRemap.a = 0f;
                result.SetColor("_ThicknessRemap", thicknessRemap);
            }
            else
            {
                result.SetFloat("_IrisDepth", irisDepth);
                result.SetFloat("_PupilScale", pupilScale);
            }
            return result;
        }



        private Material BakeHairMaterial(Material mat, string sourceName)
        {
            Texture2D diffuse = GetMaterialTexture(mat, "_DiffuseMap");
            Texture2D mask = GetMaterialTexture(mat, "_MaskMap");
            Texture2D normal = GetMaterialTexture(mat, "_NormalMap", true);
            Texture2D blend = GetMaterialTexture(mat, "_BlendMap");
            Texture2D flow = GetMaterialTexture(mat, "_FlowMap");
            Texture2D id = GetMaterialTexture(mat, "_IDMap");
            Texture2D root = GetMaterialTexture(mat, "_RootMap");
            Texture2D specular = GetMaterialTexture(mat, "_SpecularMap");
            float aoStrength = mat.GetFloat("_AOStrength");
            float aoOccludeAll = mat.GetFloat("_AOOccludeAll");
            float diffuseStrength = mat.GetFloat("_DiffuseStrength");
            float blendStrength = mat.GetFloat("_BlendStrength");
            float vertexColorStrength = mat.GetFloat("_VertexColorStrength");
            float baseColorStrength = mat.GetFloat("_BaseColorStrength");
            float alphaPower = mat.GetFloat("_AlphaPower");
            float alphaRemap = mat.GetFloat("_AlphaRemap");
            float alphaClip = mat.GetFloat("_AlphaClip");
            float shadowClip = mat.GetFloat("_ShadowClip");
            float depthPrepass = mat.GetFloat("_DepthPrepass");
            float depthPostpass = mat.GetFloat("_DepthPostpass");
            float smoothnessMin = mat.GetFloat("_SmoothnessMin");
            float smoothnessMax = mat.GetFloat("_SmoothnessMax");
            float smoothnessPower = mat.GetFloat("_SmoothnessPower");
            float globalStrength = mat.GetFloat("_GlobalStrength");
            float rootColorStrength = mat.GetFloat("_RootColorStrength");
            float endColorStrength = mat.GetFloat("_EndColorStrength");
            float invertRootMap = mat.GetFloat("_InvertRootMap");
            float highlightAStrength = mat.GetFloat("_HighlightAStrength");
            float highlightAOverlapEnd = mat.GetFloat("_HighlightAOverlapEnd");
            float highlightAOverlapInvert = mat.GetFloat("_HighlightAOverlapInvert");
            float highlightBStrength = mat.GetFloat("_HighlightBStrength");
            float highlightBOverlapEnd = mat.GetFloat("_HighlightBOverlapEnd");
            float highlightBOverlapInvert = mat.GetFloat("_HighlightBOverlapInvert");
            float rimTransmissionIntensity = mat.GetFloat("_RimTransmissionIntensity");
            float specularMultiplier = mat.GetFloat("_SpecularMultiplier");
            float specularShift = mat.GetFloat("_SpecularShift");
            float secondarySpecularMultiplier = mat.GetFloat("_SecondarySpecularMultiplier");
            float secondarySpecularShift = mat.GetFloat("_SecondarySpecularShift");
            float secondarySmoothness = mat.GetFloat("_SecondarySmoothness");
            float normalStrength = mat.GetFloat("_NormalStrength");
            Vector4 highlightADistribution = mat.GetVector("_HighlightADistribution");
            Vector4 highlightBDistribution = mat.GetVector("_HighlightBDistribution");
            Color vertexBaseColor = mat.GetColor("_VertexBaseColor");
            Color rootColor = mat.GetColor("_RootColor");
            Color endColor = mat.GetColor("_EndColor");
            Color highlightAColor = mat.GetColor("_HighlightAColor");
            Color highlightBColor = mat.GetColor("_HighlightBColor");
            Color specularTint = mat.GetColor("_SpecularTint");
            bool enableColor = mat.GetFloat("BOOLEAN_ENABLECOLOR") > 0f;            
             
            Texture2D bakedBaseMap = diffuse;
            Texture2D bakedMaskMap = mask;
            Texture2D bakedNormalMap = normal;

            if (enableColor && diffuse && id && root)
            {
                bakedBaseMap = BakeHairDiffuseMap(diffuse, blend, id, root, mask,
                    diffuseStrength, alphaPower, alphaRemap, aoStrength, aoOccludeAll,
                    rootColor, rootColorStrength, endColor, endColorStrength, globalStrength, 
                    invertRootMap, baseColorStrength,
                    highlightAColor, highlightADistribution, highlightAOverlapEnd, 
                    highlightAOverlapInvert, highlightAStrength,
                    highlightBColor, highlightBDistribution, highlightBOverlapEnd, 
                    highlightBOverlapInvert, highlightBStrength,
                    blendStrength, vertexBaseColor, vertexColorStrength,
                    sourceName + "_BaseMap");                
            }
            else if (diffuse)
            {
                bakedBaseMap = BakeHairDiffuseMap(diffuse, blend, mask,
                    diffuseStrength, alphaPower, alphaRemap, aoStrength, aoOccludeAll,
                    blendStrength, vertexBaseColor, vertexColorStrength,
                    sourceName + "_BaseMap");
            }

            if (mask)
                bakedMaskMap = BakeHairMaskMap(mask, specular,
                    aoStrength, smoothnessMin, smoothnessMax, smoothnessPower,
                    sourceName + "_Mask");

            Material result = CreateBakedMaterial(bakedBaseMap, bakedMaskMap, bakedNormalMap,
                null, null, null,
                1,
                sourceName, 
                Pipeline.GetTemplateMaterial(MaterialType.Hair, 
                            MaterialQuality.Baked, characterInfo));

            if (characterInfo.bakeCustomShaders)
            {
                result.SetTexture("_FlowMap", flow);
                result.SetColor("_VertexBaseColor", vertexBaseColor);
                result.SetFloat("_VertexColorStrength", vertexColorStrength);
                result.SetColor("_SpecularTint", specularTint);
                result.SetFloat("_AlphaClip", alphaClip);
                result.SetFloat("_ShadowClip", shadowClip);
                result.SetFloat("_DepthPrepass", depthPrepass); // Mathf.Lerp(depthPrepass, 1.0f, 0.5f));
                result.SetFloat("_DepthPostpass", depthPostpass);
                result.SetFloat("_RimTransmissionIntensity", rimTransmissionIntensity);
                result.SetFloat("_SpecularMultiplier", specularMultiplier);
                result.SetFloat("_SpecularShift", specularShift);
                result.SetFloat("_SecondarySpecularMultiplier", secondarySpecularMultiplier);
                result.SetFloat("_SecondarySpecularShift", secondarySpecularShift);
                result.SetFloat("_SecondarySmoothness", secondarySmoothness);
                result.SetFloat("_NormalStrength", normalStrength);
            }
            else
            {
                result.SetColor("_SpecularColor", specularTint);
                result.SetFloat("_AlphaClipThreshold", alphaClip);
                result.SetFloat("_AlphaThresholdShadow", shadowClip);
                result.SetFloat("_AlphaClipThresholdDepthPrepass", depthPrepass); // Mathf.Lerp(depthPrepass, 1.0f, 0.5f));
                result.SetFloat("_AlphaClipThresholdDepthPostpass", depthPostpass);
                result.SetFloat("_TransmissionRim", rimTransmissionIntensity);
                result.SetFloat("_Specular", specularMultiplier);
                result.SetFloat("_SpecularShift", specularShift);
                result.SetFloat("_SecondarySpecular", secondarySpecularMultiplier);
                result.SetFloat("_SecondarySpecularShift", secondarySpecularShift);                
                result.SetFloat("_NormalStrength", normalStrength);
                result.SetFloat("_SmoothnessMin", smoothnessMin);
                result.SetFloat("_SmoothnessMax", smoothnessMax);
            }

            return result;
        }

        private Material BakeEyeOcclusionMaterial(Material mat, string sourceName)
        {            
            float occlusionStrength = mat.GetFloat("_OcclusionStrength");
            float occlusionPower = mat.GetFloat("_OcclusionPower");
            float topMin = mat.GetFloat("_TopMin");
            float topMax = mat.GetFloat("_TopMax");
            float topCurve = mat.GetFloat("_TopCurve");
            float bottomMin = mat.GetFloat("_BottomMin");
            float bottomMax = mat.GetFloat("_BottomMax");
            float bottomCurve = mat.GetFloat("_BottomCurve");
            float innerMin = mat.GetFloat("_InnerMin");
            float innerMax = mat.GetFloat("_InnerMax");
            float outerMin = mat.GetFloat("_OuterMin");
            float outerMax = mat.GetFloat("_OuterMax");
            float occlusionStrength2 = mat.GetFloat("_OcclusionStrength2");
            float top2Min = mat.GetFloat("_Top2Min");
            float top2Max = mat.GetFloat("_Top2Max");
            float tearDuctPosition = mat.GetFloat("_TearDuctPosition");
            float tearDuctWidth = mat.GetFloat("_TearDuctWidth");
            Color occlusionColor = mat.GetColor("_OcclusionColor");

            float expandOut = mat.GetFloat("_ExpandOut");
            float expandUpper = mat.GetFloat("_ExpandUpper");
            float expandLower = mat.GetFloat("_ExpandLower");
            float expandInner = mat.GetFloat("_ExpandInner");
            float expandOuter = mat.GetFloat("_ExpandOuter");

            Texture2D bakedBaseMap = null;
            Texture2D bakedMaskMap = null;
            Texture2D bakedNormalMap = null;
            Texture2D bakedDetailMap = null;
            Texture2D bakedSubsurfaceMap = null;
            Texture2D bakedThicknessMap = null;

            bakedBaseMap = BakeEyeOcclusionDiffuseMap(
                occlusionStrength, occlusionPower, topMin, topMax, topCurve, bottomMin, bottomMax, bottomCurve,
                innerMin, innerMax, outerMin, outerMax, occlusionStrength2, top2Min, top2Max, 
                tearDuctPosition, tearDuctWidth, occlusionColor,                
                sourceName + "_BaseMap");

            Material result = CreateBakedMaterial(bakedBaseMap, bakedMaskMap, bakedNormalMap,
                bakedDetailMap, bakedSubsurfaceMap, bakedThicknessMap, 1.0f,                  
                sourceName, Pipeline.GetTemplateMaterial(MaterialType.EyeOcclusion,
                                            MaterialQuality.Baked, characterInfo));

            result.SetFloat("_ExpandOut", expandOut);
            result.SetFloat("_ExpandUpper", expandUpper);
            result.SetFloat("_ExpandLower", expandLower);
            result.SetFloat("_ExpandInner", expandInner);
            result.SetFloat("_ExpandOuter", expandOuter);

            return result;
        }


        private Texture2D BakeDiffuseBlendMap(Texture2D diffuse, Texture2D blend,
            float blendStrength,
            string name)
        {
            Vector2Int maxSize = GetMaxSize(diffuse);
            ComputeBakeTexture bakeTarget =
                new ComputeBakeTexture(maxSize, texturesFolder, name, Importer.FLAG_SRGB);

            ComputeShader bakeShader = Util.FindComputeShader(COMPUTE_SHADER);
            if (bakeShader)
            {
                diffuse = CheckDiffuse(diffuse);
                blend = CheckOverlay(blend);

                int kernel = bakeShader.FindKernel("RLDiffuseBlend");
                bakeTarget.Create(bakeShader, kernel);
                bakeShader.SetTexture(kernel, "Diffuse", diffuse);
                bakeShader.SetTexture(kernel, "ColorBlend", blend);
                bakeShader.SetFloat("colorBlendStrength", blendStrength);
                bakeShader.Dispatch(kernel, bakeTarget.width, bakeTarget.height, 1);
                return bakeTarget.SaveAndReimport();
            }

            return null;
        }

        private Texture2D BakeMaskMap(Texture2D mask,
            float aoStrength, float smoothnessMin, float smoothnessMax, float smoothnessPower,
            float microNormalStrength,
            string name)
        {
            Vector2Int maxSize = GetMaxSize(mask);
            ComputeBakeTexture bakeTarget =
                new ComputeBakeTexture(maxSize, texturesFolder, name);

            ComputeShader bakeShader = Util.FindComputeShader(COMPUTE_SHADER);
            if (bakeShader)
            {
                mask = CheckHDRP(mask);

                int kernel = bakeShader.FindKernel("RLMask");
                bakeTarget.Create(bakeShader, kernel);
                bakeShader.SetTexture(kernel, "Mask", mask);
                bakeShader.SetFloat("aoStrength", aoStrength);
                bakeShader.SetFloat("smoothnessMin", smoothnessMin);
                bakeShader.SetFloat("smoothnessMax", smoothnessMax);
                bakeShader.SetFloat("smoothnessPower", smoothnessPower);
                bakeShader.SetFloat("microNormalStrength", microNormalStrength);
                bakeShader.Dispatch(kernel, bakeTarget.width, bakeTarget.height, 1);
                return bakeTarget.SaveAndReimport();
            }

            return null;
        }

        private Texture2D BakeHeadDiffuseMap(Texture2D diffuse, Texture2D blend, Texture2D cavityAO,            
            float blendStrength,
            float mouthAOPower, float nostrilAOPower, float lipsAOPower,
            string name)
        {
            Vector2Int maxSize = GetMaxSize(diffuse);
            ComputeBakeTexture bakeTarget =
                new ComputeBakeTexture(maxSize, texturesFolder, name, Importer.FLAG_SRGB);

            ComputeShader bakeShader = Util.FindComputeShader(COMPUTE_SHADER);
            if (bakeShader)
            {
                diffuse = CheckDiffuse(diffuse);
                blend = CheckOverlay(blend);
                cavityAO = CheckMask(cavityAO);

                int kernel = bakeShader.FindKernel("RLHeadDiffuse");
                bakeTarget.Create(bakeShader, kernel);
                bakeShader.SetTexture(kernel, "Diffuse", diffuse);
                bakeShader.SetTexture(kernel, "ColorBlend", blend);
                bakeShader.SetTexture(kernel, "CavityAO", cavityAO);
                bakeShader.SetFloat("colorBlendStrength", blendStrength);
                bakeShader.SetFloat("mouthAOPower", mouthAOPower);
                bakeShader.SetFloat("nostrilAOPower", nostrilAOPower);
                bakeShader.SetFloat("lipsAOPower", lipsAOPower);
                bakeShader.Dispatch(kernel, bakeTarget.width, bakeTarget.height, 1);
                return bakeTarget.SaveAndReimport();
            }

            return null;
        }

        private Texture2D BakeHeadMaskMap(Texture2D mask, Texture2D cavityAO,
            Texture2D NMUIL, Texture2D CFULC, Texture2D earNeck,
            float aoStrength, float smoothnessMin, float smoothnessMax, float smoothnessPower, float microNormalStrength,
            float mouthAOPower, float nostrilAOPower, float lipsAOPower, float microSmoothnessMod,
            float noseMSM, float mouthMSM, float upperLidMSM, float innerLidMSM, float earMSM,
            float neckMSM, float cheekMSM, float foreheadMSM, float upperLipMSM, float chinMSM, float unmaskedMSM,            
            string name)
        {
            Vector2Int maxSize = GetMaxSize(mask);
            ComputeBakeTexture bakeTarget =
                new ComputeBakeTexture(maxSize, texturesFolder, name);

            ComputeShader bakeShader = Util.FindComputeShader(COMPUTE_SHADER);
            if (bakeShader)
            {
                mask = CheckHDRP(mask);
                cavityAO = CheckMask(cavityAO);
                NMUIL = CheckBlank(NMUIL);
                CFULC = CheckBlank(CFULC);
                earNeck = CheckBlank(earNeck);

                int kernel = bakeShader.FindKernel("RLHeadMask");
                bakeTarget.Create(bakeShader, kernel);
                bakeShader.SetTexture(kernel, "Mask", mask);
                bakeShader.SetTexture(kernel, "CavityAO", cavityAO);
                bakeShader.SetTexture(kernel, "NMUILMask", NMUIL);
                bakeShader.SetTexture(kernel, "CFULCMask", CFULC);
                bakeShader.SetTexture(kernel, "EarNeckMask", earNeck);
                bakeShader.SetFloat("aoStrength", aoStrength);
                bakeShader.SetFloat("smoothnessMin", smoothnessMin);
                bakeShader.SetFloat("smoothnessMax", smoothnessMax);
                bakeShader.SetFloat("smoothnessPower", smoothnessPower);
                bakeShader.SetFloat("microNormalStrength", microNormalStrength);
                bakeShader.SetFloat("mouthAOPower", mouthAOPower);
                bakeShader.SetFloat("nostrilAOPower", nostrilAOPower);
                bakeShader.SetFloat("lipsAOPower", lipsAOPower);
                bakeShader.SetFloat("microSmoothnessMod", microSmoothnessMod);
                bakeShader.SetFloat("noseMSM", noseMSM);
                bakeShader.SetFloat("mouthMSM", mouthMSM);
                bakeShader.SetFloat("upperLidMSM", upperLidMSM);
                bakeShader.SetFloat("innerLidMSM", innerLidMSM);
                bakeShader.SetFloat("earMSM", earMSM);
                bakeShader.SetFloat("neckMSM", neckMSM);
                bakeShader.SetFloat("cheekMSM", cheekMSM);
                bakeShader.SetFloat("foreheadMSM", foreheadMSM);
                bakeShader.SetFloat("upperLipMSM", upperLipMSM);
                bakeShader.SetFloat("chinMSM", chinMSM);
                bakeShader.SetFloat("unmaskedMSM", unmaskedMSM);                
                bakeShader.Dispatch(kernel, bakeTarget.width, bakeTarget.height, 1);
                return bakeTarget.SaveAndReimport();
            }

            return null;
        }

        private Texture2D BakeSkinMaskMap(Texture2D mask, Texture2D RGBA,
            float aoStrength, float smoothnessMin, float smoothnessMax, float smoothnessPower, 
            float microNormalStrength, float microSmoothnessMod,
            float rMSM, float gMSM, float bMSM, float aMSM, float unmaskedMSM,            
            string name)
        {
            Vector2Int maxSize = GetMaxSize(mask);
            ComputeBakeTexture bakeTarget =
                new ComputeBakeTexture(maxSize, texturesFolder, name);

            ComputeShader bakeShader = Util.FindComputeShader(COMPUTE_SHADER);
            if (bakeShader)
            {
                mask = CheckHDRP(mask);
                RGBA = CheckBlank(RGBA);                

                int kernel = bakeShader.FindKernel("RLSkinMask");
                bakeTarget.Create(bakeShader, kernel);
                bakeShader.SetTexture(kernel, "Mask", mask);
                bakeShader.SetTexture(kernel, "RGBAMask", RGBA);
                bakeShader.SetFloat("aoStrength", aoStrength);
                bakeShader.SetFloat("smoothnessMin", smoothnessMin);
                bakeShader.SetFloat("smoothnessMax", smoothnessMax);
                bakeShader.SetFloat("smoothnessPower", smoothnessPower);
                bakeShader.SetFloat("microNormalStrength", microNormalStrength);
                bakeShader.SetFloat("microSmoothnessMod", microSmoothnessMod);
                bakeShader.SetFloat("rMSM", rMSM);
                bakeShader.SetFloat("gMSM", gMSM);
                bakeShader.SetFloat("bMSM", bMSM);
                bakeShader.SetFloat("aMSM", aMSM);
                bakeShader.SetFloat("unmaskedMSM", unmaskedMSM);
                bakeShader.Dispatch(kernel, bakeTarget.width, bakeTarget.height, 1);
                return bakeTarget.SaveAndReimport();
            }

            return null;
        }

        private Texture2D BakeHeadSubsurfaceMap(Texture2D subsurface, 
            Texture2D NMUIL, Texture2D CFULC, Texture2D earNeck,
            float subsurfaceScale,
            float noseSS, float mouthSS, float upperLidSS, float innerLidSS, float earSS,
            float neckSS, float cheekSS, float foreheadSS, float upperLipSS, float chinSS, float unmaskedSS,
            string name)
        {
            Vector2Int maxSize = GetMaxSize(subsurface);
            if (maxSize.x > 1024) maxSize.x = 1024;
            if (maxSize.y > 1024) maxSize.y = 1024;
            ComputeBakeTexture bakeTarget =
                new ComputeBakeTexture(maxSize, texturesFolder, name);

            ComputeShader bakeShader = Util.FindComputeShader(COMPUTE_SHADER);
            if (bakeShader)
            {
                subsurface = CheckMask(subsurface);
                NMUIL = CheckBlank(NMUIL);
                CFULC = CheckBlank(CFULC);
                earNeck = CheckBlank(earNeck);

                int kernel = bakeShader.FindKernel("RLHeadSubsurface");
                bakeTarget.Create(bakeShader, kernel);
                bakeShader.SetTexture(kernel, "Subsurface", subsurface);
                bakeShader.SetTexture(kernel, "NMUILMask", NMUIL);
                bakeShader.SetTexture(kernel, "CFULCMask", CFULC);
                bakeShader.SetTexture(kernel, "EarNeckMask", earNeck);
                bakeShader.SetFloat("subsurfaceScale", subsurfaceScale);
                bakeShader.SetFloat("noseSS", noseSS);
                bakeShader.SetFloat("mouthSS", mouthSS);
                bakeShader.SetFloat("upperLidSS", upperLidSS);
                bakeShader.SetFloat("innerLidSS", innerLidSS);
                bakeShader.SetFloat("earSS", earSS);
                bakeShader.SetFloat("neckSS", neckSS);
                bakeShader.SetFloat("cheekSS", cheekSS);
                bakeShader.SetFloat("foreheadSS", foreheadSS);
                bakeShader.SetFloat("upperLipSS", upperLipSS);
                bakeShader.SetFloat("chinSS", chinSS);
                bakeShader.SetFloat("unmaskedSS", unmaskedSS);
                bakeShader.Dispatch(kernel, bakeTarget.width, bakeTarget.height, 1);
                return bakeTarget.SaveAndReimport();
            }

            return null;
        }

        private Texture2D BakeSkinSubsurfaceMap(Texture2D subsurface, Texture2D RGBA,
            float subsurfaceScale,            
            float rSS, float gSS, float bSS, float aSS, float unmaskedSS,
            string name)
        {
            Vector2Int maxSize = GetMaxSize(subsurface);
            if (maxSize.x > 1024) maxSize.x = 1024;
            if (maxSize.y > 1024) maxSize.y = 1024;
            ComputeBakeTexture bakeTarget =
                new ComputeBakeTexture(maxSize, texturesFolder, name);

            ComputeShader bakeShader = Util.FindComputeShader(COMPUTE_SHADER);
            if (bakeShader)
            {
                subsurface = CheckMask(subsurface);
                RGBA = CheckBlank(RGBA);

                int kernel = bakeShader.FindKernel("RLSkinSubsurface");
                bakeTarget.Create(bakeShader, kernel);
                bakeShader.SetTexture(kernel, "Subsurface", subsurface);
                bakeShader.SetTexture(kernel, "RGBAMask", RGBA);
                bakeShader.SetFloat("subsurfaceScale", subsurfaceScale);
                bakeShader.SetFloat("rSS", rSS);
                bakeShader.SetFloat("gSS", gSS);
                bakeShader.SetFloat("bSS", bSS);
                bakeShader.SetFloat("aSS", aSS);
                bakeShader.SetFloat("unmaskedSS", unmaskedSS);
                bakeShader.Dispatch(kernel, bakeTarget.width, bakeTarget.height, 1);
                return bakeTarget.SaveAndReimport();
            }

            return null;
        }

        private Texture2D BakeBlendNormalMap(Texture2D normal, Texture2D normalBlend,
            float normalBlendStrength,
            string name)
        {
            Vector2Int maxSize = GetMaxSize(normal, normalBlend);
            ComputeBakeTexture bakeTarget =
                new ComputeBakeTexture(maxSize, texturesFolder, name, Importer.FLAG_NORMAL);

            ComputeShader bakeShader = Util.FindComputeShader(COMPUTE_SHADER);
            if (bakeShader)
            {
                normal = CheckNormal(normal);
                normalBlend = CheckNormal(normalBlend);

                int kernel = bakeShader.FindKernel("RLNormalBlend");
                bakeTarget.Create(bakeShader, kernel);
                bakeShader.SetTexture(kernel, "Normal", normal);
                bakeShader.SetTexture(kernel, "NormalBlend", normalBlend);
                bakeShader.SetFloat("normalBlendStrength", normalBlendStrength);
                bakeShader.Dispatch(kernel, bakeTarget.width, bakeTarget.height, 1);
                return bakeTarget.SaveAndReimport();
            }

            return null;
        }

        private Texture2D BakeDetailMap(Texture2D microNormal,
            string name)
        {
            Vector2Int maxSize = GetMaxSize(microNormal);
            ComputeBakeTexture bakeTarget =
                new ComputeBakeTexture(maxSize, texturesFolder, name);

            ComputeShader bakeShader = Util.FindComputeShader(COMPUTE_SHADER);
            if (bakeShader)
            {
                microNormal = CheckNormal(microNormal);

                int kernel = bakeShader.FindKernel("RLDetail");
                bakeTarget.Create(bakeShader, kernel);
                bakeShader.SetTexture(kernel, "MicroNormal", microNormal);
                bakeShader.Dispatch(kernel, bakeTarget.width, bakeTarget.height, 1);
                return bakeTarget.SaveAndReimport();
            }

            return null;
        }


        private Texture2D BakeSubsurfaceMap(Texture2D subsurface,
            float subsurfaceScale,
            string name)
        {
            Vector2Int maxSize = GetMaxSize(subsurface);
            if (maxSize.x > 1024) maxSize.x = 1024;
            if (maxSize.y > 1024) maxSize.y = 1024;
            ComputeBakeTexture bakeTarget =
                new ComputeBakeTexture(maxSize, texturesFolder, name);

            ComputeShader bakeShader = Util.FindComputeShader(COMPUTE_SHADER);
            if (bakeShader)
            {
                subsurface = CheckMask(subsurface);

                int kernel = bakeShader.FindKernel("RLSubsurface");
                bakeTarget.Create(bakeShader, kernel);
                bakeShader.SetTexture(kernel, "Subsurface", subsurface);
                bakeShader.SetFloat("subsurfaceScale", subsurfaceScale);
                bakeShader.Dispatch(kernel, bakeTarget.width, bakeTarget.height, 1);
                return bakeTarget.SaveAndReimport();
            }

            return null;
        }

        private Texture2D BakeThicknessMap(Texture2D thickness,
            float thicknessScale,
            string name)
        {
            Vector2Int maxSize = GetMaxSize(thickness);
            if (maxSize.x > 1024) maxSize.x = 1024;
            if (maxSize.y > 1024) maxSize.y = 1024;
            ComputeBakeTexture bakeTarget =
                new ComputeBakeTexture(maxSize, texturesFolder, name);

            ComputeShader bakeShader = Util.FindComputeShader(COMPUTE_SHADER);
            if (bakeShader)
            {
                thickness = CheckMask(thickness);

                int kernel = bakeShader.FindKernel("RLThickness");
                bakeTarget.Create(bakeShader, kernel);
                bakeShader.SetTexture(kernel, "Thickness", thickness);
                bakeShader.SetFloat("thicknessScale", thicknessScale);
                bakeShader.Dispatch(kernel, bakeTarget.width, bakeTarget.height, 1);
                return bakeTarget.SaveAndReimport();
            }

            return null;
        }

        private Texture2D BakeTeethDiffuseMap(Texture2D diffuse, Texture2D gumsMask, Texture2D gradientAO,
            float isUpperTeeth, float frontAO, float rearAO, float gumsSaturation, float gumsBrightness,
            float teethSaturation, float teethBrightness,
            string name)
        {
            Vector2Int maxSize = GetMaxSize(diffuse);
            ComputeBakeTexture bakeTarget =
                new ComputeBakeTexture(maxSize, texturesFolder, name, Importer.FLAG_SRGB);

            ComputeShader bakeShader = Util.FindComputeShader(COMPUTE_SHADER);
            if (bakeShader)
            {
                diffuse = CheckDiffuse(diffuse);
                gumsMask = CheckMask(gumsMask);
                gradientAO = CheckMask(gradientAO);

                int kernel = bakeShader.FindKernel("RLTeethDiffuse");
                bakeTarget.Create(bakeShader, kernel);
                bakeShader.SetTexture(kernel, "Diffuse", diffuse);
                bakeShader.SetTexture(kernel, "GumsMask", gumsMask);
                bakeShader.SetTexture(kernel, "GradientAO", gradientAO);
                bakeShader.SetFloat("isUpperTeeth", isUpperTeeth);
                bakeShader.SetFloat("frontAO", frontAO);
                bakeShader.SetFloat("rearAO", rearAO);
                bakeShader.SetFloat("gumsSaturation", gumsSaturation);
                bakeShader.SetFloat("gumsBrightness", gumsBrightness);
                bakeShader.SetFloat("teethSaturation", teethSaturation);
                bakeShader.SetFloat("teethBrightness", teethBrightness);
                bakeShader.Dispatch(kernel, bakeTarget.width, bakeTarget.height, 1);
                return bakeTarget.SaveAndReimport();
            }

            return null;
        }

        private Texture2D BakeTeethMaskMap(Texture2D mask, Texture2D gradientAO,
            float isUpperTeeth, float aoStrength, float smoothnessMin, float smoothnessMax, float smoothnessPower,
            float frontAO, float rearAO, float microNormalStrength,
            string name)
        {
            Vector2Int maxSize = GetMaxSize(mask);
            ComputeBakeTexture bakeTarget =
                new ComputeBakeTexture(maxSize, texturesFolder, name);

            ComputeShader bakeShader = Util.FindComputeShader(COMPUTE_SHADER);
            if (bakeShader)
            {
                mask = CheckHDRP(mask);
                gradientAO = CheckMask(gradientAO);

                int kernel = bakeShader.FindKernel("RLTeethMask");
                bakeTarget.Create(bakeShader, kernel);
                bakeShader.SetTexture(kernel, "Mask", mask);
                bakeShader.SetTexture(kernel, "GradientAO", gradientAO);
                bakeShader.SetFloat("aoStrength", aoStrength);
                bakeShader.SetFloat("smoothnessMin", smoothnessMin);
                bakeShader.SetFloat("smoothnessMax", smoothnessMax);
                bakeShader.SetFloat("smoothnessPower", smoothnessPower);
                bakeShader.SetFloat("microNormalStrength", microNormalStrength);
                bakeShader.SetFloat("isUpperTeeth", isUpperTeeth);
                bakeShader.SetFloat("frontAO", frontAO);
                bakeShader.SetFloat("rearAO", rearAO);
                bakeShader.Dispatch(kernel, bakeTarget.width, bakeTarget.height, 1);
                return bakeTarget.SaveAndReimport();
            }

            return null;
        }

        private Texture2D BakeTeethSubsurfaceMap(Texture2D gumsMask, Texture2D gradientAO,
            float isUpperTeeth, float rearAO, float frontAO, float gumsSSS, float teethSSS,
            string name)
        {
            Vector2Int maxSize = new Vector2Int(256, 256);
            ComputeBakeTexture bakeTarget =
                new ComputeBakeTexture(maxSize, texturesFolder, name);

            ComputeShader bakeShader = Util.FindComputeShader(COMPUTE_SHADER);
            if (bakeShader)
            {
                gumsMask = CheckMask(gumsMask);
                gradientAO = CheckMask(gradientAO);

                int kernel = bakeShader.FindKernel("RLTeethSubsurface");
                bakeTarget.Create(bakeShader, kernel);
                bakeShader.SetTexture(kernel, "GumsMask", gumsMask);
                bakeShader.SetTexture(kernel, "GradientAO", gradientAO);
                bakeShader.SetFloat("isUpperTeeth", isUpperTeeth);
                bakeShader.SetFloat("rearAO", rearAO);
                bakeShader.SetFloat("frontAO", frontAO);
                bakeShader.SetFloat("gumsSSS", gumsSSS);
                bakeShader.SetFloat("teethSSS", teethSSS);
                bakeShader.Dispatch(kernel, bakeTarget.width, bakeTarget.height, 1);
                return bakeTarget.SaveAndReimport();
            }

            return null;
        }
        private Texture2D BakeTeethThicknessMap(Texture2D gumsMask,
            float gumsThickness, float teethThickness,
            string name)
        {
            Vector2Int maxSize = new Vector2Int(256, 256);
            ComputeBakeTexture bakeTarget =
                new ComputeBakeTexture(maxSize, texturesFolder, name);

            ComputeShader bakeShader = Util.FindComputeShader(COMPUTE_SHADER);
            if (bakeShader)
            {
                gumsMask = CheckMask(gumsMask);

                int kernel = bakeShader.FindKernel("RLTeethThickness");
                bakeTarget.Create(bakeShader, kernel);
                bakeShader.SetTexture(kernel, "GumsMask", gumsMask);
                bakeShader.SetFloat("gumsThickness", gumsThickness);
                bakeShader.SetFloat("teethThickness", teethThickness);
                bakeShader.Dispatch(kernel, bakeTarget.width, bakeTarget.height, 1);
                return bakeTarget.SaveAndReimport();
            }

            return null;
        }


        private Texture2D BakeTongueDiffuseMap(Texture2D diffuse, Texture2D gradientAO,
            float frontAO, float rearAO, float tongueSaturation, float tongueBrightness,
            string name)
        {
            Vector2Int maxSize = GetMaxSize(diffuse);
            ComputeBakeTexture bakeTarget =
                new ComputeBakeTexture(maxSize, texturesFolder, name, Importer.FLAG_SRGB);

            ComputeShader bakeShader = Util.FindComputeShader(COMPUTE_SHADER);
            if (bakeShader)
            {
                diffuse = CheckDiffuse(diffuse);
                gradientAO = CheckMask(gradientAO);

                int kernel = bakeShader.FindKernel("RLTongueDiffuse");
                bakeTarget.Create(bakeShader, kernel);
                bakeShader.SetTexture(kernel, "Diffuse", diffuse);
                bakeShader.SetTexture(kernel, "GradientAO", gradientAO);
                bakeShader.SetFloat("frontAO", frontAO);
                bakeShader.SetFloat("rearAO", rearAO);
                bakeShader.SetFloat("tongueSaturation", tongueSaturation);
                bakeShader.SetFloat("tongueBrightness", tongueBrightness);
                bakeShader.Dispatch(kernel, bakeTarget.width, bakeTarget.height, 1);
                return bakeTarget.SaveAndReimport();
            }

            return null;
        }

        private Texture2D BakeTongueMaskMap(Texture2D mask, Texture2D gradientAO,
            float aoStrength, float smoothnessMin, float smoothnessMax, float smoothnessPower,
            float frontAO, float rearAO, float microNormalStrength,
            string name)
        {
            Vector2Int maxSize = GetMaxSize(mask);
            ComputeBakeTexture bakeTarget =
                new ComputeBakeTexture(maxSize, texturesFolder, name);

            ComputeShader bakeShader = Util.FindComputeShader(COMPUTE_SHADER);
            if (bakeShader)
            {
                mask = CheckHDRP(mask);
                gradientAO = CheckMask(gradientAO);

                int kernel = bakeShader.FindKernel("RLTongueMask");
                bakeTarget.Create(bakeShader, kernel);
                bakeShader.SetTexture(kernel, "Mask", mask);
                bakeShader.SetTexture(kernel, "GradientAO", gradientAO);
                bakeShader.SetFloat("aoStrength", aoStrength);
                bakeShader.SetFloat("smoothnessMin", smoothnessMin);
                bakeShader.SetFloat("smoothnessMax", smoothnessMax);
                bakeShader.SetFloat("smoothnessPower", smoothnessPower);
                bakeShader.SetFloat("microNormalStrength", microNormalStrength);
                bakeShader.SetFloat("frontAO", frontAO);
                bakeShader.SetFloat("rearAO", rearAO);
                bakeShader.Dispatch(kernel, bakeTarget.width, bakeTarget.height, 1);
                return bakeTarget.SaveAndReimport();
            }

            return null;
        }

        private Texture2D BakeTongueSubsurfaceMap(Texture2D gradientAO,
            float rearAO, float frontAO, float tongueSSS,
            string name)
        {
            Vector2Int maxSize = new Vector2Int(256, 256);
            ComputeBakeTexture bakeTarget =
                new ComputeBakeTexture(maxSize, texturesFolder, name);

            ComputeShader bakeShader = Util.FindComputeShader(COMPUTE_SHADER);
            if (bakeShader)
            {
                gradientAO = CheckMask(gradientAO);

                int kernel = bakeShader.FindKernel("RLTongueSubsurface");
                bakeTarget.Create(bakeShader, kernel);
                bakeShader.SetTexture(kernel, "GradientAO", gradientAO);
                bakeShader.SetFloat("rearAO", rearAO);
                bakeShader.SetFloat("frontAO", frontAO);
                bakeShader.SetFloat("tongueSSS", tongueSSS);
                bakeShader.Dispatch(kernel, bakeTarget.width, bakeTarget.height, 1);
                return bakeTarget.SaveAndReimport();
            }

            return null;
        }

        private Texture2D BakeCorneaDiffuseMap(Texture2D sclera, Texture2D colorBlend,
            float scleraScale, float scleraHue, float scleraSaturation, float scleraBrightness,
            float irisScale, float irisRadius, float limbusWidth, float shadowRadius, float shadowHardness,
            Color cornerShadowColor, float colorBlendStrength,
            string name)
        {
            Vector2Int maxSize = GetMaxSize(sclera);
            ComputeBakeTexture bakeTarget =
                new ComputeBakeTexture(maxSize, texturesFolder, name, Importer.FLAG_SRGB);

            ComputeShader bakeShader = Util.FindComputeShader(COMPUTE_SHADER);
            if (bakeShader)
            {
                sclera = CheckDiffuse(sclera);
                colorBlend = CheckOverlay(colorBlend);

                int kernel = bakeShader.FindKernel("RLCorneaDiffuse");
                bakeTarget.Create(bakeShader, kernel);
                bakeShader.SetTexture(kernel, "ScleraDiffuse", sclera);
                bakeShader.SetTexture(kernel, "ColorBlend", colorBlend);
                bakeShader.SetFloat("scleraScale", scleraScale);
                bakeShader.SetFloat("scleraHue", scleraHue);
                bakeShader.SetFloat("scleraSaturation", scleraSaturation);
                bakeShader.SetFloat("scleraBrightness", scleraBrightness);
                bakeShader.SetFloat("irisScale", irisScale);
                bakeShader.SetFloat("irisRadius", irisRadius);
                bakeShader.SetFloat("limbusWidth", limbusWidth);
                bakeShader.SetFloat("shadowRadius", shadowRadius);
                bakeShader.SetFloat("shadowHardness", shadowHardness);
                bakeShader.SetFloat("colorBlendStrength", colorBlendStrength);
                bakeShader.SetVector("cornerShadowColor", cornerShadowColor);
                bakeShader.Dispatch(kernel, bakeTarget.width, bakeTarget.height, 1);
                return bakeTarget.SaveAndReimport();
            }

            return null;
        }

        private Texture2D BakeEyeDiffuseMap(Texture2D cornea, Texture2D colorBlend,
            float irisScale, float irisHue, float irisSaturation, float irisBrightness,
            float limbusDarkRadius, float limbusDarkWidth, Color limbusColor,
            float colorBlendStrength,
            string name)
        {
            Vector2Int maxSize = GetMaxSize(cornea);
            ComputeBakeTexture bakeTarget =
                new ComputeBakeTexture(maxSize, texturesFolder, name, Importer.FLAG_SRGB);

            ComputeShader bakeShader = Util.FindComputeShader(COMPUTE_SHADER);
            if (bakeShader)
            {
                cornea = CheckDiffuse(cornea);
                colorBlend = CheckOverlay(colorBlend);

                int kernel = bakeShader.FindKernel("RLEyeDiffuse");
                bakeTarget.Create(bakeShader, kernel);
                bakeShader.SetTexture(kernel, "CorneaDiffuse", cornea);
                bakeShader.SetTexture(kernel, "ColorBlend", colorBlend);
                bakeShader.SetFloat("irisScale", irisScale);
                bakeShader.SetFloat("irisHue", irisHue);
                bakeShader.SetFloat("irisSaturation", irisSaturation);
                bakeShader.SetFloat("irisBrightness", irisBrightness);
                bakeShader.SetFloat("limbusDarkRadius", limbusDarkRadius);
                bakeShader.SetFloat("limbusDarkWidth", limbusDarkWidth);
                bakeShader.SetFloat("colorBlendStrength", colorBlendStrength);
                bakeShader.SetVector("limbusColor", limbusColor);
                bakeShader.Dispatch(kernel, bakeTarget.width, bakeTarget.height, 1);
                return bakeTarget.SaveAndReimport();
            }

            return null;
        }

        private Texture2D BakeCorneaMaskMap(Texture2D mask,
            float aoStrength, float corneaSmoothness, float scleraSmoothness,
            float irisScale, float irisRadius, float limbusWidth, float microNormalStrength,
            string name)
        {
            Vector2Int maxSize = GetMaxSize(mask);
            ComputeBakeTexture bakeTarget =
                new ComputeBakeTexture(maxSize, texturesFolder, name);

            ComputeShader bakeShader = Util.FindComputeShader(COMPUTE_SHADER);
            if (bakeShader)
            {
                mask = CheckHDRP(mask);

                int kernel = bakeShader.FindKernel("RLCorneaMask");
                bakeTarget.Create(bakeShader, kernel);
                bakeShader.SetTexture(kernel, "Mask", mask);
                bakeShader.SetFloat("aoStrength", aoStrength);
                bakeShader.SetFloat("corneaSmoothness", corneaSmoothness);
                bakeShader.SetFloat("scleraSmoothness", scleraSmoothness);
                bakeShader.SetFloat("microNormalStrength", microNormalStrength);
                bakeShader.SetFloat("irisScale", irisScale);
                bakeShader.SetFloat("irisRadius", irisRadius);
                bakeShader.SetFloat("limbusWidth", limbusWidth);
                bakeShader.Dispatch(kernel, bakeTarget.width, bakeTarget.height, 1);
                return bakeTarget.SaveAndReimport();
            }

            return null;
        }

        private Texture2D BakeEyeMaskMap(Texture2D mask,
            float aoStrength, float irisSmoothness, float scleraSmoothness,
            float irisScale, float limbusDarkRadius, float limbusDarkWidth,
            float irisRadius, float depthRadius,
            string name)
        {
            Vector2Int maxSize = GetMaxSize(mask);
            ComputeBakeTexture bakeTarget =
                new ComputeBakeTexture(maxSize, texturesFolder, name);

            ComputeShader bakeShader = Util.FindComputeShader(COMPUTE_SHADER);
            if (bakeShader)
            {
                mask = CheckHDRP(mask);

                int kernel = bakeShader.FindKernel("RLEyeMask");
                bakeTarget.Create(bakeShader, kernel);
                bakeShader.SetTexture(kernel, "Mask", mask);
                bakeShader.SetFloat("aoStrength", aoStrength);
                bakeShader.SetFloat("irisSmoothness", irisSmoothness);
                bakeShader.SetFloat("scleraSmoothness", scleraSmoothness);
                bakeShader.SetFloat("irisScale", irisScale);
                bakeShader.SetFloat("limbusDarkRadius", limbusDarkRadius);
                bakeShader.SetFloat("limbusDarkWidth", limbusDarkWidth);
                bakeShader.SetFloat("irisRadius", irisRadius);
                bakeShader.SetFloat("depthRadius", depthRadius);
                bakeShader.Dispatch(kernel, bakeTarget.width, bakeTarget.height, 1);
                return bakeTarget.SaveAndReimport();
            }

            return null;
        }

        private Texture2D BakeCorneaThicknessMap(
            float irisScale, float limbusDarkRadius, float limbusDarkWidth, float thicknessScale,
            string name)
        {
            Vector2Int maxSize = new Vector2Int(256, 256);
            ComputeBakeTexture bakeTarget =
                new ComputeBakeTexture(maxSize, texturesFolder, name);

            ComputeShader bakeShader = Util.FindComputeShader(COMPUTE_SHADER);
            if (bakeShader)
            {
                int kernel = bakeShader.FindKernel("RLCorneaThickness");
                bakeTarget.Create(bakeShader, kernel);
                bakeShader.SetFloat("irisScale", irisScale);
                bakeShader.SetFloat("limbusDarkRadius", limbusDarkRadius);
                bakeShader.SetFloat("limbusDarkWidth", limbusDarkWidth);
                bakeShader.SetFloat("thicknessScale", thicknessScale);
                bakeShader.Dispatch(kernel, bakeTarget.width, bakeTarget.height, 1);
                return bakeTarget.SaveAndReimport();
            }

            return null;
        }

        private Texture2D BakeHairDiffuseMap(Texture2D diffuse, Texture2D blend, Texture2D id, Texture2D root, Texture2D mask,
                        float diffuseStrength, float alphaPower, float alphaRemap, float aoStrength, float aoOccludeAll,
                        Color rootColor, float rootColorStrength, Color endColor, float endColorStrength, float globalStrength, 
                        float invertRootMap, float baseColorStrength,
                        Color highlightAColor, Vector4 highlightADistribution, float highlightAOverlapEnd, 
                        float highlightAOverlapInvert, float highlightAStrength,
                        Color highlightBColor, Vector4 highlightBDistribution, float highlightBOverlapEnd, 
                        float highlightBOverlapInvert, float highlightBStrength,
                        float blendStrength, Color vertexBaseColor, float vertexColorStrength,
                        string name)
        {
            Vector2Int maxSize = GetMaxSize(diffuse, id);
            ComputeBakeTexture bakeTarget =
                new ComputeBakeTexture(maxSize, texturesFolder, name, 
                    Importer.FLAG_SRGB + 
                    (name.iContains("hair") ? Importer.FLAG_HAIR : Importer.FLAG_ALPHA_CLIP)
                );

            ComputeShader bakeShader = Util.FindComputeShader(COMPUTE_SHADER);
            if (bakeShader)
            {
                diffuse = CheckDiffuse(diffuse);
                blend = CheckMask(blend);
                id = CheckOverlay(id);
                root = CheckMask(root);

                int kernel = bakeShader.FindKernel("RLHairColoredDiffuse");
                bakeTarget.Create(bakeShader, kernel);
                bakeShader.SetTexture(kernel, "Diffuse", diffuse);
                bakeShader.SetTexture(kernel, "ColorBlend", blend);
                bakeShader.SetTexture(kernel, "ID", id);
                bakeShader.SetTexture(kernel, "Root", root);
                bakeShader.SetTexture(kernel, "Mask", mask);
                bakeShader.SetFloat("diffuseStrength", diffuseStrength);
                bakeShader.SetFloat("alphaPower", alphaPower);
                bakeShader.SetFloat("alphaRemap", alphaRemap);
                bakeShader.SetFloat("aoStrength", aoStrength);
                bakeShader.SetFloat("aoOccludeAll", aoOccludeAll);
                bakeShader.SetFloat("rootColorStrength", rootColorStrength);
                bakeShader.SetFloat("endColorStrength", endColorStrength);
                bakeShader.SetFloat("globalStrength", globalStrength);
                bakeShader.SetFloat("invertRootMap", invertRootMap);
                bakeShader.SetFloat("baseColorStrength", baseColorStrength);
                bakeShader.SetFloat("highlightAOverlapEnd", highlightAOverlapEnd);
                bakeShader.SetFloat("highlightAOverlapInvert", highlightAOverlapInvert);
                bakeShader.SetFloat("highlightAStrength", highlightAStrength);
                bakeShader.SetFloat("highlightBOverlapEnd", highlightBOverlapEnd);
                bakeShader.SetFloat("highlightBOverlapInvert", highlightBOverlapInvert);
                bakeShader.SetFloat("highlightBStrength", highlightBStrength);
                bakeShader.SetFloat("colorBlendStrength", blendStrength);
                bakeShader.SetFloat("vertexColorStrength", vertexColorStrength);
                bakeShader.SetVector("rootColor", rootColor);
                bakeShader.SetVector("endColor", endColor);
                bakeShader.SetVector("highlightAColor", highlightAColor);
                bakeShader.SetVector("highlightBColor", highlightBColor);
                bakeShader.SetVector("vertexBaseColor", vertexBaseColor);
                bakeShader.SetVector("highlightADistribution", highlightADistribution);
                bakeShader.SetVector("highlightBDistribution", highlightBDistribution);
                bakeShader.Dispatch(kernel, bakeTarget.width, bakeTarget.height, 1);
                return bakeTarget.SaveAndReimport();
            }

            return null;
        }

        private Texture2D BakeHairDiffuseMap(Texture2D diffuse, Texture2D blend, Texture2D mask,
                        float diffuseStrength, float alphaPower, float alphaRemap, float aoStrength, float aoOccludeAll,
                        float blendStrength, Color vertexBaseColor, float vertexColorStrength,
                        string name)
        {
            Vector2Int maxSize = GetMaxSize(diffuse);
            ComputeBakeTexture bakeTarget =
                new ComputeBakeTexture(maxSize, texturesFolder, name,
                    Importer.FLAG_SRGB +
                    (name.iContains("hair") ? Importer.FLAG_HAIR : Importer.FLAG_ALPHA_CLIP)
                );

            ComputeShader bakeShader = Util.FindComputeShader(COMPUTE_SHADER);
            if (bakeShader)
            {
                diffuse = CheckDiffuse(diffuse);
                blend = CheckMask(blend);

                int kernel = bakeShader.FindKernel("RLHairDiffuse");
                bakeTarget.Create(bakeShader, kernel);
                bakeShader.SetTexture(kernel, "Diffuse", diffuse);
                bakeShader.SetTexture(kernel, "ColorBlend", blend);
                bakeShader.SetTexture(kernel, "Mask", mask);
                bakeShader.SetFloat("diffuseStrength", diffuseStrength);
                bakeShader.SetFloat("alphaPower", alphaPower);
                bakeShader.SetFloat("alphaRemap", alphaRemap);
                bakeShader.SetFloat("aoStrength", aoStrength);
                bakeShader.SetFloat("aoOccludeAll", aoOccludeAll);
                bakeShader.SetFloat("colorBlendStrength", blendStrength);
                bakeShader.SetFloat("vertexColorStrength", vertexColorStrength);
                bakeShader.SetVector("vertexBaseColor", vertexBaseColor);
                bakeShader.Dispatch(kernel, bakeTarget.width, bakeTarget.height, 1);
                return bakeTarget.SaveAndReimport();
            }

            return null;
        }

        private Texture2D BakeHairMaskMap(Texture2D mask, Texture2D specular,
            float aoStrength, float smoothnessMin, float smoothnessMax, float smoothnessPower,            
            string name)
        {
            Vector2Int maxSize = GetMaxSize(mask);
            ComputeBakeTexture bakeTarget =
                new ComputeBakeTexture(maxSize, texturesFolder, name);

            ComputeShader bakeShader = Util.FindComputeShader(COMPUTE_SHADER);
            if (bakeShader)
            {
                mask = CheckHDRP(mask);
                specular = CheckMask(specular);

                int kernel = bakeShader.FindKernel("RLHairMask");
                bakeTarget.Create(bakeShader, kernel);
                bakeShader.SetTexture(kernel, "Mask", mask);
                bakeShader.SetTexture(kernel, "Specular", specular);
                bakeShader.SetFloat("aoStrength", aoStrength);
                bakeShader.SetFloat("smoothnessMin", smoothnessMin);
                bakeShader.SetFloat("smoothnessMax", smoothnessMax);
                bakeShader.SetFloat("smoothnessPower", smoothnessPower);                
                bakeShader.Dispatch(kernel, bakeTarget.width, bakeTarget.height, 1);
                return bakeTarget.SaveAndReimport();
            }

            return null;
        }

        private Texture2D BakeEyeOcclusionDiffuseMap(float occlusionStrength, float occlusionPower, 
            float topMin, float topMax, float topCurve, float bottomMin, float bottomMax, float bottomCurve,
            float innerMin, float innerMax, float outerMin, float outerMax, 
            float occlusionStrength2, float top2Min, float top2Max,
            float tearDuctPosition, float tearDuctWidth, Color occlusionColor,
            string name)
        {
            Vector2Int maxSize = new Vector2Int(256, 256);
            ComputeBakeTexture bakeTarget =
                new ComputeBakeTexture(maxSize, texturesFolder, name, Importer.FLAG_SRGB);

            ComputeShader bakeShader = Util.FindComputeShader(COMPUTE_SHADER);
            if (bakeShader)
            {
                int kernel = bakeShader.FindKernel("RLEyeOcclusionDiffuse");
                bakeTarget.Create(bakeShader, kernel);
                bakeShader.SetFloat("eoOcclusionStrength", occlusionStrength);
                bakeShader.SetFloat("eoOcclusionPower", occlusionPower);
                bakeShader.SetFloat("eoTopMin", topMin);
                bakeShader.SetFloat("eoTopMax", topMax);
                bakeShader.SetFloat("eoTopCurve", topCurve);
                bakeShader.SetFloat("eoBottomMin", bottomMin);
                bakeShader.SetFloat("eoBottomMax", bottomMax);
                bakeShader.SetFloat("eoBottomCurve", bottomCurve);
                bakeShader.SetFloat("eoInnerMin", innerMin);
                bakeShader.SetFloat("eoInnerMax", innerMax);
                bakeShader.SetFloat("eoOuterMin", outerMin);
                bakeShader.SetFloat("eoOuterMax", outerMax);
                bakeShader.SetFloat("eoOcclusionStrength2", occlusionStrength2);
                bakeShader.SetFloat("eoTop2Min", top2Min);
                bakeShader.SetFloat("eoTop2Max", top2Max);
                bakeShader.SetFloat("eoTearDuctPosition", tearDuctPosition);
                bakeShader.SetFloat("eoTearDuctWidth", tearDuctWidth);
                bakeShader.SetVector("eoEyeOcclusionColor", occlusionColor);
                bakeShader.Dispatch(kernel, bakeTarget.width, bakeTarget.height, 1);
                return bakeTarget.SaveAndReimport();
            }

            return null;
        }


        public Texture2D BakeDefaultSkinThicknessMap(Texture2D thickness, string name)
        {
            Texture2D bakedThickness = BakeThicknessMap(thickness, 1.0f, name);            
            return bakedThickness;
        }

        public Texture2D BakeDefaultDetailMap(Texture2D microNormal, string name)
        {
            Texture2D bakedDetail = BakeDetailMap(microNormal, name);
            return bakedDetail;
        }

    }

}