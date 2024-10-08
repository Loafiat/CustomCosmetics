﻿#define RELEASE
using BepInEx;
using BepInEx.Configuration;
using Photon.Pun;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using GorillaNetworking;
using HarmonyLib;
using CustomCosmetics.Patches;
using System.Threading.Tasks;
using System.Collections;
using BananaOS;
using System.Text;

namespace CustomCosmetics
{
    /// <summary>
    /// This is your mod's main class.
    /// </summary>

    [BepInPlugin(PluginInfo.GUID, PluginInfo.Name, PluginInfo.Version)]
    public class Plugin : BaseUnityPlugin
    {
        public static Plugin instance;
        GameObject currentRHoldable;
        GameObject currentLHoldable;
        GameObject currentHat;
        GameObject currentBadge;
        customMaterial currentMaterial;
        customMaterial currentTaggedMaterial;
        Material defaultTaggedMaterial;
        public string cosmeticPath = Path.Combine(Path.GetDirectoryName(typeof(Plugin).Assembly.Location), "Cosmetics");
        public ConfigEntry<string> hat;
        public ConfigEntry<string> Lholdable;
        public ConfigEntry<string> Rholdable;
        public ConfigEntry<string> badge;
        public ConfigEntry<string> material;
        public ConfigEntry<string> taggedMaterial;
        public ConfigEntry<bool> removeCosmetics;
        Dictionary<Photon.Realtime.Player, GameObject> networkHats = new Dictionary<Photon.Realtime.Player, GameObject>();
        Dictionary<Photon.Realtime.Player, GameObject> networkRHoldables = new Dictionary<Photon.Realtime.Player, GameObject>();
        Dictionary<Photon.Realtime.Player, GameObject> networkLHoldables = new Dictionary<Photon.Realtime.Player, GameObject>();
        Dictionary<Photon.Realtime.Player, GameObject> networkBadges = new Dictionary<Photon.Realtime.Player, GameObject>();
        Dictionary<VRRig, Photon.Realtime.Player> cosmeticsplayers = new Dictionary<VRRig, Photon.Realtime.Player>();
        Dictionary<VRRig, Photon.Realtime.Player> normalplayers = new Dictionary<VRRig, Photon.Realtime.Player>();
        Dictionary<string, GameObject> assetCache = new Dictionary<string, GameObject>();
        Dictionary<string, GameObject> nameAssetCache = new Dictionary<string, GameObject>();
        public int prevMatIndex;
        public bool assetsLoaded = false;
        public bool loadError = false;
        public StringBuilder errorText;
        public string brokenCosmetic;

        // General Cosmetic Info Values
        public string cosmeticName;
        public string cosmeticAuthor;
        public string cosmeticDescription;
        public string currentCosmeticFile;

        // Holdable Cosmetic Values
        public bool leftHand;

        // Material Cosmetic Values
        public bool materialCustomColours;

        // Old Method Check
        public bool usingTextMethod;

        // New Exporters
        public MaterialDescriptor matDes;
        public BadgeDescriptor badgeDes;
        public HatDescriptor hatDes;
        public HoldableDescriptor holdableDes;

        void Awake()
        {
            SceneManager.sceneLoaded += GameInitialized;
            instance = this;
        }

        void GameInitialized(Scene scene, LoadSceneMode loadMode)
        {
            if (scene.name == "GorillaTag")
            {
                /* Code here runs after the game initializes (i.e. GorillaLocomotion.Player.Instance != null) */
                currentTaggedMaterial.mat = null;
                currentMaterial.mat = null;
                removeCosmetics = Config.Bind("Settings", "Remove Cosmetics", false, "Whether the mod should unequip normal cosmetics when equipping custom ones.");
                hat = Config.Bind("Cosmetics", "Current Hat", String.Empty, "This is the current hat your using.");
                Lholdable = Config.Bind("Cosmetics", "Current Left Holdable", string.Empty, "This is the current left holdable your using.");
                Rholdable = Config.Bind("Cosmetics", "Current Right Holdable", string.Empty, "This is the current right holdable your using.");
                badge = Config.Bind("Cosmetics", "Current Badge", string.Empty, "This is the current badge your using.");
                material = Config.Bind("Cosmetics", "Current Material", string.Empty, "This is the current material your using.");
                taggedMaterial = Config.Bind("Cosmetics", "Current Tagged Material", string.Empty, "This is the current tagged material your using.");
                if (!Directory.Exists(cosmeticPath))
                {
                    Directory.CreateDirectory(cosmeticPath);
                }
                if (!Directory.Exists(cosmeticPath + "/Hats"))
                {
                    Directory.CreateDirectory(cosmeticPath + "/Hats");
                }
                if (!Directory.Exists(cosmeticPath + "/Holdables"))
                {
                    Directory.CreateDirectory(cosmeticPath + "/Holdables");
                }
                if (!Directory.Exists(cosmeticPath + "/Badges"))
                {
                    Directory.CreateDirectory(cosmeticPath + "/Badges");
                }
                if (!Directory.Exists(cosmeticPath + "/Materials"))
                {
                    Directory.CreateDirectory(cosmeticPath + "/Materials");
                }
                this.AddComponent<Net>();
                Harmony harmony = Harmony.CreateAndPatchAll(typeof(Plugin).Assembly, PluginInfo.GUID);
                Type rigCache = typeof(GorillaTagger).Assembly.GetType("VRRigCache");
                harmony.Patch(AccessTools.Method(rigCache, "AddRigToGorillaParent"), postfix: new HarmonyMethod(typeof(RigJoinPatch), nameof(RigJoinPatch.Patch)));
                harmony.Patch(AccessTools.Method(rigCache, "RemoveRigFromGorillaParent"), prefix: new HarmonyMethod(typeof(RigLeavePatch), nameof(RigLeavePatch.Patch)));
                GorillaTagger.Instance.offlineVRRig.OnColorChanged += UpdateColour;
                LoadAssets();
            }
        }

        public async Task LoadAssets()
        {
            string currentCosmeticLoading = "null";
            loadError = false;
            try
            {
                Debug.Log("Loading Custom Cosmetics");
                GameObject cosmeticsParent = new GameObject("CustomCosmetics");
                foreach (string hat in Directory.GetFiles(cosmeticPath + "/Hats/", "*.hat"))
                {
                    currentCosmeticLoading = hat;
                    AssetBundle hatbundle = await LoadBundle(hat);
                    GameObject temphat = hatbundle.LoadAsset<GameObject>("hat");
                    temphat.transform.SetParent(cosmeticsParent.transform);
                    hatbundle.Unload(false);
                    assetCache.TryAdd(Path.GetFileName(hat), temphat);
                    string hatname = GetCosName(temphat, "Hat");
                    nameAssetCache.Add(hatname, temphat);
                }
                foreach (string holdable in Directory.GetFiles(cosmeticPath + "/Holdables/", "*.holdable"))
                {
                    currentCosmeticLoading = holdable;
                    AssetBundle holdablebundle = await LoadBundle(holdable);
                    GameObject tempholdable = holdablebundle.LoadAsset<GameObject>("holdABLE");
                    tempholdable.transform.SetParent(cosmeticsParent.transform);
                    holdablebundle.Unload(false);
                    assetCache.TryAdd(Path.GetFileName(holdable), tempholdable);
                    string holdablename = GetCosName(tempholdable, "Holdable");
                    nameAssetCache.Add(holdablename, tempholdable);
                }
                foreach (string badge in Directory.GetFiles(cosmeticPath + "/Badges/", "*.badge"))
                {
                    currentCosmeticLoading = badge;
                    AssetBundle badgebundle = await LoadBundle(badge);
                    GameObject tempbadge = badgebundle.LoadAsset<GameObject>("badge");
                    tempbadge.transform.SetParent(cosmeticsParent.transform);
                    badgebundle.Unload(false);
                    assetCache.TryAdd(Path.GetFileName(badge), tempbadge);
                    string badgename = GetCosName(tempbadge, "Badge");
                    nameAssetCache.Add(badgename, tempbadge);
                }
                foreach (string material in Directory.GetFiles(cosmeticPath + "/Materials/", "*.material"))
                {
                    currentCosmeticLoading = material;
                    AssetBundle materialbundle = await LoadBundle(material);
                    GameObject tempmaterial = materialbundle.LoadAsset<GameObject>("material");
                    tempmaterial.transform.SetParent(cosmeticsParent.transform);
                    materialbundle.Unload(false);
                    assetCache.TryAdd(Path.GetFileName(material), tempmaterial);
                    string matname = GetCosName(tempmaterial, "Material");
                    nameAssetCache.Add(matname, tempmaterial);
                }
                defaultTaggedMaterial = GorillaTagger.Instance.offlineVRRig.materialsToChangeTo[2];
                string savedhat = hat.Value;
                string savedlholdable = Lholdable.Value;
                string savedrholdable = Rholdable.Value;
                string savedbadge = badge.Value;
                string savedmaterial = material.Value;
                string savedtagmaterial = taggedMaterial.Value;
                if (File.Exists(cosmeticPath + "/Hats/" + savedhat))
                {
                    GetInfo(savedhat, "Hat");
                    LoadHat(cosmeticPath + "/Hats/" + savedhat);
                }
                if (File.Exists(cosmeticPath + "/Holdables/" + savedrholdable))
                {
                    GetInfo(savedrholdable, "Holdable");
                    LoadHoldable(cosmeticPath + "/Holdables/" + savedrholdable, false);
                }
                if (File.Exists(cosmeticPath + "/Holdables/" + savedlholdable))
                {
                    GetInfo(savedlholdable, "Holdable");
                    LoadHoldable(cosmeticPath + "/Holdables/" + savedlholdable, true);
                }
                if (File.Exists(cosmeticPath + "/Badges/" + savedbadge))
                {
                    GetInfo(savedbadge, "Badge");
                    LoadBadge(cosmeticPath + "/Badges/" + savedbadge);
                }
                if (File.Exists(cosmeticPath + "/Materials/" + savedmaterial))
                {
                    GetInfo(savedmaterial, "Material");
                    LoadMaterial(cosmeticPath + "/Materials/" + savedmaterial, 0);
                }
                if (File.Exists(cosmeticPath + "/Materials/" + savedtagmaterial))
                {
                    GetInfo(savedtagmaterial, "Material");
                    LoadMaterial(cosmeticPath + "/Materials/" + savedtagmaterial, 2);
                }
                assetsLoaded = true;
                MonkeWatch.Instance.UpdateScreen();
                BananaNotifications.DisplayNotification("<align=center><size=2><b>Finished Loading Custom Cosmetics!\n Have Fun!</b></size></align>", new Color(0.424f, 0.086f, 0.839f, 1f), Color.white, 2f);
                Debug.Log("Finished Loading Custom Cosmetics");
            }
            catch(Exception ex)
            {
                brokenCosmetic = currentCosmeticLoading;
                Debug.Log("Issue when loading CustomCosmetics");
                StringBuilder str = new StringBuilder();
                str.AppendLine("<color=red>==Error When Loading==</color>");
                str.AppendLine(string.Empty);
                str.AppendLine("There was an error when loading cosmetics.");
                str.AppendLine($"You have a broken cosmetic installed, \nplease click enter to delete cosmetic {Path.GetFileName(currentCosmeticLoading)} and reload the mod");
                errorText = str;
                assetCache.Clear();
                nameAssetCache.Clear();

                Debug.LogError(ex.Message);
                loadError = true;
                MonkeWatch.Instance.UpdateScreen();
                BananaNotifications.DisplayErrorNotification("<align=center><size=2><b>Error when loading Custom Cosmetics\n Please check the Cosmetics page on the watch for more info</b></size></align>", 5f);
            }
            
        }

        public void LoadHoldable(string file, bool lHand)
        {
            if (file == "DisableR")
            {
                Destroy(currentRHoldable);
                Rholdable.Value = string.Empty;
                var table = PhotonNetwork.LocalPlayer.CustomProperties;
                table["CustomRHoldable"] = string.Empty;
                PhotonNetwork.LocalPlayer.SetCustomProperties(table);
                return;
            }
            if (file == "DisableL")
            {
                Destroy(currentLHoldable);
                Lholdable.Value = string.Empty;
                var table = PhotonNetwork.LocalPlayer.CustomProperties;
                table["CustomLHoldable"] = string.Empty;
                PhotonNetwork.LocalPlayer.SetCustomProperties(table);
                return;
            }

            if(!assetCache.TryGetValue(Path.GetFileName(file), out var empty))
            {
                return;
            }

            GameObject asset;
            assetCache.TryGetValue(Path.GetFileName(file), out asset);
            GameObject prefab = Instantiate(asset);
            if (prefab != null)
            {
                var parentAsset = prefab;
                if (!usingTextMethod)
                {
                    foreach (Collider collider in parentAsset.GetComponentsInChildren<Collider>())
                    {
                        Destroy(collider);
                    }
                    if (!lHand)
                    {
                        GameObject rHoldable = Instantiate(holdableDes.rightHandObject);
                        if (holdableDes.behaviours.Count > 0)
                        {
                            foreach (CosmeticBehaviour behaviour in holdableDes.behaviours)
                            {
                                CustomBehaviour cbehaviour = behaviour.gameObject.AddComponent<CustomBehaviour>();
                                cbehaviour.button = behaviour.button;
                                foreach (GameObject o in behaviour.objectsToToggle)
                                {
                                    cbehaviour.objectsToToggle.Add(rHoldable.transform.FindChildRecursive(o.name).gameObject);
                                }
                            }
                        }
                        Destroy(currentRHoldable);
                        currentRHoldable = rHoldable;
                        Rholdable.Value = Path.GetFileName(file);
                        var table = PhotonNetwork.LocalPlayer.CustomProperties;
                        table["CustomRHoldable"] = holdableDes.Name;
                        PhotonNetwork.LocalPlayer.SetCustomProperties(table);
                        rHoldable.transform.SetParent(GorillaTagger.Instance.offlineVRRig.headMesh.transform.parent.Find("shoulder.R/upper_arm.R/forearm.R/hand.R/palm.01.R/").transform, false);
                    }
                    else if (lHand)
                    {
                        GameObject lHoldable = Instantiate(holdableDes.leftHandObject);
                        if (holdableDes.behaviours.Count > 0)
                        {
                            foreach (CosmeticBehaviour behaviour in holdableDes.behaviours)
                            {
                                CustomBehaviour cbehaviour = behaviour.gameObject.AddComponent<CustomBehaviour>();
                                cbehaviour.button = behaviour.button;
                                foreach (GameObject o in behaviour.objectsToToggle)
                                {
                                    cbehaviour.objectsToToggle.Add(lHoldable.transform.FindChildRecursive(o.name).gameObject);
                                }
                            }
                        }
                        Destroy(currentLHoldable);
                        currentLHoldable = lHoldable;
                        Lholdable.Value = Path.GetFileName(file);
                        var table = PhotonNetwork.LocalPlayer.CustomProperties;
                        table["CustomLHoldable"] = holdableDes.Name;
                        PhotonNetwork.LocalPlayer.SetCustomProperties(table);
                        lHoldable.transform.SetParent(GorillaTagger.Instance.offlineVRRig.headMesh.transform.parent.Find("shoulder.L/upper_arm.L/forearm.L/hand.L/palm.01.L/").transform, false);
                    }
                    Destroy(parentAsset);
                }
                else
                {
                    foreach (Collider collider in parentAsset.GetComponentsInChildren<Collider>())
                    {
                        Destroy(collider);
                    }
                    if (!lHand)
                    {
                        Destroy(currentRHoldable);
                        currentRHoldable = parentAsset;
                        Rholdable.Value = Path.GetFileName(file);
                        var table = PhotonNetwork.LocalPlayer.CustomProperties;
                        table["CustomRHoldable"] = cosmeticName;
                        PhotonNetwork.LocalPlayer.SetCustomProperties(table);
                        parentAsset.transform.SetParent(GorillaTagger.Instance.offlineVRRig.headMesh.transform.parent.Find("shoulder.R/upper_arm.R/forearm.R/hand.R/palm.01.R/").transform, false);
                    }
                    else if (lHand)
                    {
                        Destroy(currentLHoldable);
                        currentLHoldable = parentAsset;
                        Lholdable.Value = Path.GetFileName(file);
                        var table = PhotonNetwork.LocalPlayer.CustomProperties;
                        table["CustomLHoldable"] = cosmeticName;
                        PhotonNetwork.LocalPlayer.SetCustomProperties(table);
                        parentAsset.transform.SetParent(GorillaTagger.Instance.offlineVRRig.headMesh.transform.parent.Find("shoulder.L/upper_arm.L/forearm.L/hand.L/palm.01.L/").transform, false);
                    }
                }
            }
        }

        public void LoadHat(string file)
        {
            if (file == "Disable")
            {
                Destroy(currentHat);
                hat.Value = string.Empty;
                var table = PhotonNetwork.LocalPlayer.CustomProperties;
                table["CustomHat"] = string.Empty;
                PhotonNetwork.LocalPlayer.SetCustomProperties(table);
                return;
            }

            if (!assetCache.ContainsKey(Path.GetFileName(file)))
                return;

            if (removeCosmetics.Value)
                RemoveItem(CosmeticsController.CosmeticCategory.Hat, CosmeticsController.CosmeticSlots.Hat);
            GameObject asset;
            assetCache.TryGetValue(Path.GetFileName(file), out asset);
            GameObject prefab = Instantiate(asset);
            hat.Value = Path.GetFileName(file);
            if (!usingTextMethod)
            {
                var table = PhotonNetwork.LocalPlayer.CustomProperties;
                table["CustomHat"] = hatDes.Name;
                PhotonNetwork.LocalPlayer.SetCustomProperties(table);
            }
            else
            {
                var table = PhotonNetwork.LocalPlayer.CustomProperties;
                table["CustomHat"] = cosmeticName;
                PhotonNetwork.LocalPlayer.SetCustomProperties(table);
            }
            if (prefab != null)
            {
                var parentAsset = prefab;
                if (!usingTextMethod)
                {
                    foreach (Collider collider in parentAsset.GetComponentsInChildren<Collider>())
                    {
                        Destroy(collider);
                    }
                    if (hatDes.behaviours.Count > 0)
                    {
                        foreach (CosmeticBehaviour behaviour in hatDes.behaviours)
                        {
                            CustomBehaviour cbehaviour = parentAsset.AddComponent<CustomBehaviour>();
                            cbehaviour.button = behaviour.button;
                            foreach (GameObject o in behaviour.objectsToToggle)
                            {
                                cbehaviour.objectsToToggle.Add(parentAsset.transform.FindChildRecursive(o.name).gameObject);
                            }
                        }
                    }
                }
                else
                {
                    foreach (Collider collider in parentAsset.GetComponentsInChildren<Collider>())
                    {
                        Destroy(collider);
                    }
                }
                if (currentHat != null)
                {
                    Destroy(currentHat);
                }
                currentHat = parentAsset;
                parentAsset.transform.SetParent(GorillaTagger.Instance.offlineVRRig.headMesh.transform, false);
            }
        }
        public void LoadBadge(string file)
        {
            if (file == "Disable")
            {
                Destroy(currentBadge);
                badge.Value = string.Empty;
                var table = PhotonNetwork.LocalPlayer.CustomProperties;
                table["CustomBadge"] = string.Empty;
                PhotonNetwork.LocalPlayer.SetCustomProperties(table);
            }

            if (!assetCache.ContainsKey(Path.GetFileName(file)))
                return;

            if (removeCosmetics.Value)
                RemoveItem(CosmeticsController.CosmeticCategory.Badge, CosmeticsController.CosmeticSlots.Badge);
            GameObject asset;
            assetCache.TryGetValue(Path.GetFileName(file), out asset);
            GameObject prefab = Instantiate(asset);
            badge.Value = Path.GetFileName(file);
            if (!usingTextMethod)
            {
                var table = PhotonNetwork.LocalPlayer.CustomProperties;
                table["CustomBadge"] = badgeDes.Name;
                PhotonNetwork.LocalPlayer.SetCustomProperties(table);
            }
            else
            {
                var table = PhotonNetwork.LocalPlayer.CustomProperties;
                table["CustomBadge"] = cosmeticName;
                PhotonNetwork.LocalPlayer.SetCustomProperties(table);
            }

            if (prefab != null)
            {
                var parentAsset = prefab;
                foreach (Collider collider in parentAsset.GetComponentsInChildren<Collider>())
                {
                    Destroy(collider);
                }
                Destroy(currentBadge);
                currentBadge = parentAsset;
                parentAsset.transform.SetParent(GorillaTagger.Instance.offlineVRRig.headMesh.transform.parent, false);
            }
        }

        // void OnGUI()
        // {
        //     GUILayout.Label("Custom Properties");
        //     GUILayout.BeginArea(new Rect(10, 10, Screen.width, 500));
        //     if (PhotonNetwork.InRoom)
        //     {
        //         foreach (Photon.Realtime.Player player in PhotonNetwork.PlayerList)
        //         {
        //             GUILayout.Label(player.NickName + player.CustomProperties.ToString());
        //         }
        //     }
        //     GUILayout.EndArea();
        // }

        public void CheckPlayer(NetPlayer player, VRRig playerRig)
        {
            try
            {
                if (!playerRig.isLocal)
                {
                    ExitGames.Client.Photon.Hashtable props = PhotonNetwork.CurrentRoom.GetPlayer(player.ActorNumber).CustomProperties;
                    Photon.Realtime.Player playerr = PhotonNetwork.CurrentRoom.GetPlayer(player.ActorNumber);
                    normalplayers.Add(playerRig, playerr);
                    Debug.Log($"{player.NickName} entered the room");
                    if (props.ContainsKey("CustomHat") || props.ContainsKey("CustomLHoldable") || props.ContainsKey("CustomRHoldable") || props.ContainsKey("CustomBadge") || props.ContainsKey("CustomMaterial") || props.ContainsKey("CustomTagMaterial"))
                    {
                        cosmeticsplayers.Add(playerRig, playerr);
                        SetCosmetics(playerRig, props, playerr);
                    }
                }
                else
                {
                    var table = PhotonNetwork.LocalPlayer.CustomProperties;
                    table["Colour"] = GorillaTagger.Instance.offlineVRRig.playerColor.ToString();
                    PhotonNetwork.LocalPlayer.SetCustomProperties(table);
                }
            }
            catch (Exception e)
            {
                Debug.Log(e.Message);
            }
        }
        public void RemovePlayer(NetPlayer p, VRRig r)
        {
            try
            {
                if (!r.isLocal)
                {
                    Photon.Realtime.Player player = normalplayers[r];
                    ExitGames.Client.Photon.Hashtable props = player.CustomProperties;
                    if (props.ContainsKey("CustomHat") || props.ContainsKey("CustomLHoldable") || props.ContainsKey("CustomRHoldable") || props.ContainsKey("CustomMaterial") || props.ContainsKey("CustomTagMaterial"))
                    {
                        RemoveCosmetics(props, r, player);
                    }
                    normalplayers.Remove(r);
                }
                else
                {
                    EnableMaterial();
                }
            }
            catch(Exception e)
            {
                Debug.Log(e.Message);
            }
        }

        public void RemoveCosmetics(ExitGames.Client.Photon.Hashtable props, VRRig r, Photon.Realtime.Player player)
        {
            if (props.ContainsKey("CustomHat") || props.ContainsKey("CustomLHoldable") || props.ContainsKey("CustomRHoldable") || props.ContainsKey("CustomBadge") || props.ContainsKey("CustomMaterial") || props.ContainsKey("CustomTagMaterial"))
            {
                if(networkHats.ContainsKey(player))
                {
                    Destroy(networkHats[player]);
                    networkHats.Remove(player);
                }
                if (networkLHoldables.ContainsKey(player))
                {
                    Destroy(networkLHoldables[player]);
                    networkLHoldables.Remove(player);
                }
                if (networkRHoldables.ContainsKey(player))
                {
                    Destroy(networkRHoldables[player]);
                    networkRHoldables.Remove(player);
                }
                if (networkBadges.ContainsKey(player))
                {
                    Destroy(networkBadges[player]);
                    networkBadges.Remove(player);
                }
                r.materialsToChangeTo[0] = r.myDefaultSkinMaterialInstance;
                r.materialsToChangeTo[2] = r.myDefaultSkinMaterialInstance;
                Material[] sharedMaterials = r.mainSkin.sharedMaterials;
                sharedMaterials[0] = r.materialsToChangeTo[r.setMatIndex];
                sharedMaterials[1] = r.defaultSkin.chestMaterial;
                r.mainSkin.sharedMaterials = sharedMaterials;
                cosmeticsplayers.Remove(r);
            }
        }

        public void SetCosmetics(VRRig playerRig, ExitGames.Client.Photon.Hashtable props, Photon.Realtime.Player playerr)
        {
            if (playerRig != null)
            {
                SetCosmetic(playerRig, props, "CustomHat", playerr);
                SetCosmetic(playerRig, props, "CustomRHoldable", playerr);
                SetCosmetic(playerRig, props, "CustomLHoldable", playerr);
                SetCosmetic(playerRig, props, "CustomBadge", playerr);
                SetCosmetic(playerRig, props, "CustomMaterial", playerr);
                SetCosmetic(playerRig, props, "CustomTagMaterial", playerr);
            }
            else
            {
                Debug.Log("rig is null uh oh");
                return;
            }
            if (playerRig.playerText.gameObject == null)
            {
                Debug.Log("text is null this is not sigma but its fine");
            }
        }

        public void SetCosmetic(VRRig playerRig, ExitGames.Client.Photon.Hashtable props, string propkey, Photon.Realtime.Player playerr)
        {
            if (props.TryGetValue(propkey, out object cosm))
            {
                string cosmetic = cosm.ToString();
                Debug.Log($"{playerr.NickName} is using Custom Cosmetics, cosmetic equipped is: {cosmetic}");
                if (nameAssetCache.ContainsKey(cosmetic))
                {
                    props.TryGetValue("Colour", out object co);
                    Color col = parseColor(co.ToString());
                    switch (propkey)
                    {
                        case "CustomMaterial":
                            LoadNetworkMaterial(cosmetic, 0, playerRig, playerr, col);
                            break;
                        case "CustomTagMaterial":
                            LoadNetworkMaterial(cosmetic, 2, playerRig, playerr, col);
                            break;
                        case "CustomBadge":
                            LoadNetworkBadge(cosmetic, playerRig, playerr);
                            break;
                        case "CustomLHoldable":
                            LoadNetworkHoldable(cosmetic, playerRig, playerr, true);
                            break;
                        case "CustomRHoldable":
                            LoadNetworkHoldable(cosmetic, playerRig, playerr, false);
                            break;
                        case "CustomHat":
                            LoadNetworkHat(cosmetic, playerRig, playerr);
                            break;
                    }
                }
            }
        }

        public void LoadNetworkHat(string file, VRRig rig, Photon.Realtime.Player player)
        {
            if (file != string.Empty)
            {
                GameObject asset;
                nameAssetCache.TryGetValue(file, out asset);
                GameObject prefab = Instantiate(asset);
                if (prefab != null)
                {
                    var parentAsset = prefab;
                    foreach (Collider collider in parentAsset.GetComponentsInChildren<Collider>())
                    {
                        Destroy(collider);
                    }
                    networkHats.Add(player, parentAsset);
                    parentAsset.transform.SetParent(rig.headMesh.transform, false);
                }
            }
        }
        public void LoadNetworkHoldable(string file, VRRig rig, Photon.Realtime.Player player, bool lHand)
        {
            try
            {
                if (file != string.Empty)
                {
                    GameObject asset;
                    nameAssetCache.TryGetValue(file, out asset);
                    GameObject prefab = Instantiate(asset);
                    if (prefab != null)
                    {
                        var parentAsset = prefab;
                        foreach (Collider collider in parentAsset.GetComponentsInChildren<Collider>())
                        {
                            Destroy(collider);
                        }
                        if(parentAsset.TryGetComponent<Text>(out var text))
                        {
                            if (!lHand)
                            {
                                parentAsset.transform.SetParent(rig.transform.Find("rig/body/shoulder.R/upper_arm.R/forearm.R/hand.R/palm.01.R/"), false);
                                networkRHoldables.Add(player, parentAsset);
                            }
                            else if (lHand)
                            {
                                parentAsset.transform.SetParent(rig.transform.Find("rig/body/shoulder.L/upper_arm.L/forearm.L/hand.L/palm.01.L/"), false);
                                networkLHoldables.Add(player, parentAsset);
                            }
                        }
                        else
                        {
                            if (!lHand)
                            {
                                GameObject rHoldable = Instantiate(parentAsset.GetComponent<HoldableDescriptor>().rightHandObject);
                                rHoldable.transform.SetParent(rig.headMesh.transform.parent.Find("shoulder.R/upper_arm.R/forearm.R/hand.R/palm.01.R/"), false);
                                networkRHoldables.Add(player, rHoldable);
                            }
                            else if (lHand)
                            {
                                GameObject lHoldable = Instantiate(parentAsset.GetComponent<HoldableDescriptor>().leftHandObject);
                                lHoldable.transform.SetParent(rig.headMesh.transform.parent.Find("shoulder.L/upper_arm.L/forearm.L/hand.L/palm.01.L/"), false);
                                networkLHoldables.Add(player, lHoldable);
                            }
                            Destroy(parentAsset);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.Log(e.Message);
            }
        }
        public void LoadNetworkBadge(string file, VRRig rig, Photon.Realtime.Player player)
        {
            if (file != string.Empty)
            {
                GameObject asset;
                nameAssetCache.TryGetValue(file, out asset);
                GameObject prefab = Instantiate(asset);
                if (prefab != null)
                {
                    var parentAsset = prefab;
                    foreach (Collider collider in parentAsset.GetComponentsInChildren<Collider>())
                    {
                        Destroy(collider);
                    }
                    parentAsset.transform.SetParent(rig.headMesh.transform.parent, false);
                    networkBadges.Add(player, parentAsset);
                }
            }
        }
        public void LoadNetworkMaterial(string file, int materialIndex, VRRig rig, Photon.Realtime.Player player, Color colour)
        {
            if (file != string.Empty)
            {
                GameObject asset;
                nameAssetCache.TryGetValue(file, out asset);
                GameObject prefab = Instantiate(asset);
                if (prefab != null)
                {
                    var parentAsset = prefab;
                    try
                    {
                        if (materialIndex == 0)
                        {
                            MaterialDescriptor matInfo = parentAsset.GetComponent<MaterialDescriptor>();
                            if (matInfo.customColors)
                            {
                                parentAsset.GetComponent<MeshRenderer>().material.color = rig.playerColor;
                            }
                            rig.materialsToChangeTo[0] = parentAsset.GetComponent<MeshRenderer>().material;
                            Material[] sharedMaterials = rig.mainSkin.sharedMaterials;
                            sharedMaterials[0] = rig.materialsToChangeTo[rig.setMatIndex];
                            sharedMaterials[1] = rig.defaultSkin.chestMaterial;
                            rig.mainSkin.sharedMaterials = sharedMaterials;
                        }
                        else if (materialIndex == 2)
                        {
                            MaterialDescriptor matInfo = parentAsset.GetComponent<MaterialDescriptor>();
                            rig.materialsToChangeTo[materialIndex] = parentAsset.GetComponent<MeshRenderer>().material;
                            Material[] sharedMaterials = rig.mainSkin.sharedMaterials;
                            sharedMaterials[0] = rig.materialsToChangeTo[rig.setMatIndex];
                            sharedMaterials[1] = rig.defaultSkin.chestMaterial;
                            rig.mainSkin.sharedMaterials = sharedMaterials;
                        }
                        else
                        {
                            rig.materialsToChangeTo[materialIndex] = currentTaggedMaterial.mat;
                        }
                        Destroy(parentAsset);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogException(ex);
                    }
                }
            }
            else
            {
                if (materialIndex == 0)
                {
                    rig.materialsToChangeTo[materialIndex] = rig.myDefaultSkinMaterialInstance;
                    Material[] sharedMaterials = rig.mainSkin.sharedMaterials;
                    sharedMaterials[0] = rig.materialsToChangeTo[rig.setMatIndex];
                    sharedMaterials[1] = rig.defaultSkin.chestMaterial;
                    rig.mainSkin.sharedMaterials = sharedMaterials;
                }
                else if (materialIndex == 2)
                {
                    rig.materialsToChangeTo[materialIndex] = defaultTaggedMaterial;
                    Material[] sharedMaterials = rig.mainSkin.sharedMaterials;
                    sharedMaterials[0] = rig.materialsToChangeTo[rig.setMatIndex];
                    sharedMaterials[1] = rig.defaultSkin.chestMaterial;
                    rig.mainSkin.sharedMaterials = sharedMaterials;
                }
            }
        }

        public async Task<AssetBundle> LoadBundle(string file)
        {
            var bundleLoadRequest = AssetBundle.LoadFromFileAsync(file);

            // AssetBundleCreateRequest is a YieldInstruction !!
            await Yield(bundleLoadRequest);

            AssetBundle _storedBundle = bundleLoadRequest.assetBundle;
            return _storedBundle;
        }

        async Task Yield(YieldInstruction instruction)
        {
            var completionSource = new TaskCompletionSource<YieldInstruction>();
            StartCoroutine(AwaitInstructionCorouutine(instruction, completionSource));
            await completionSource.Task;
        }

        IEnumerator AwaitInstructionCorouutine(YieldInstruction instruction, TaskCompletionSource<YieldInstruction> completionSource)
        {
            yield return instruction;
            completionSource.SetResult(instruction);
        }

        public void CheckItems()
        {
            if(removeCosmetics.Value == true)
            {
                var items = CosmeticsController.instance.currentWornSet.items;
                for (int i = 0; i < items.Length; i++)
                {
                    if (items[i].itemCategory == CosmeticsController.CosmeticCategory.Hat)
                    {
                        LoadHat("Disable");
                    }
                    if (items[i].itemCategory == CosmeticsController.CosmeticCategory.Badge)
                    {
                        LoadBadge("Disable");
                    }
                }
            }
        }

        public static void RemoveItem(CosmeticsController.CosmeticCategory category, CosmeticsController.CosmeticSlots slot)
        {
            try
            {
                bool updateCart = false;

                var nullItem = CosmeticsController.instance.nullItem;

                var items = CosmeticsController.instance.currentWornSet.items;
                for (int i = 0; i < items.Length; i++)
                {
                    if (items[i].itemCategory == category && !items[i].isNullItem)
                    {
                        updateCart = true;
                        items[i] = nullItem;
                    }
                }

                items = CosmeticsController.instance.tryOnSet.items;
                for (int i = 0; i < items.Length; i++)
                {
                    if (items[i].itemCategory == category && !items[i].isNullItem)
                    {
                        updateCart = true;
                        items[i] = nullItem;
                    }
                }

                // TODO: Check if this call is necessary
                if (updateCart)
                {
                    CosmeticsController.instance.UpdateShoppingCart();
                    CosmeticsController.instance.UpdateWornCosmetics(true);

                    PlayerPrefs.SetString(CosmeticsController.CosmeticSet.SlotPlayerPreferenceName(slot), nullItem.itemName);
                    PlayerPrefs.Save();
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to remove game cosmetic\n{e.GetType().Name} ({e.Message})");
            }
        }

        class Net : MonoBehaviourPunCallbacks
        {
            public override void OnPlayerPropertiesUpdate(Photon.Realtime.Player targetPlayer, ExitGames.Client.Photon.Hashtable changedProps)
            {
                base.OnPlayerPropertiesUpdate(targetPlayer, changedProps);

                if (targetPlayer.IsLocal) return;

                NetPlayer player = NetworkSystem.Instance.GetPlayer(targetPlayer.ActorNumber);
                Plugin.instance.RemoveCosmetics(changedProps, GorillaGameManager.instance.FindPlayerVRRig(targetPlayer), targetPlayer);
                Plugin.instance.SetCosmetics(GorillaGameManager.instance.FindPlayerVRRig(targetPlayer), changedProps, targetPlayer);
            }
        }

        public void LoadMaterial(string file, int materialIndex)
        {
            if (file == "Disable")
            {
                if(materialIndex == 0)
                {
                    material.Value = string.Empty;
                    currentMaterial.mat = null;
                    var table = PhotonNetwork.LocalPlayer.CustomProperties;
                    table["CustomMaterial"] = string.Empty;
                    PhotonNetwork.LocalPlayer.SetCustomProperties(table);
                    VRRig rig = GorillaTagger.Instance.offlineVRRig;
                    rig.materialsToChangeTo[0] = rig.myDefaultSkinMaterialInstance;
                    Material[] sharedMaterials = rig.mainSkin.sharedMaterials;
                    sharedMaterials[0] = rig.materialsToChangeTo[rig.setMatIndex];
                    sharedMaterials[1] = rig.defaultSkin.chestMaterial;
                    rig.mainSkin.sharedMaterials = sharedMaterials;
                }
                else if (materialIndex == 2)
                {
                    taggedMaterial.Value = string.Empty;
                    currentTaggedMaterial.mat = null;
                    var table = PhotonNetwork.LocalPlayer.CustomProperties;
                    table["CustomTagMaterial"] = string.Empty;
                    PhotonNetwork.LocalPlayer.SetCustomProperties(table);
                    VRRig rig = GorillaTagger.Instance.offlineVRRig;
                    rig.materialsToChangeTo[2] = defaultTaggedMaterial;
                    Material[] sharedMaterials = rig.mainSkin.sharedMaterials;
                    sharedMaterials[0] = rig.materialsToChangeTo[rig.setMatIndex];
                    sharedMaterials[1] = rig.defaultSkin.chestMaterial;
                    rig.mainSkin.sharedMaterials = sharedMaterials;
                }
            }

            if (!assetCache.TryGetValue(Path.GetFileName(file), out var empty))
            {
                return;
            }

            if (materialIndex == 0)
            {
                material.Value = Path.GetFileName(file);
            }
            else if (materialIndex == 2)
            {
                taggedMaterial.Value = Path.GetFileName(file);
            }
            GameObject asset;
            assetCache.TryGetValue(Path.GetFileName(file), out asset);
            GameObject prefab = Instantiate(asset);
            RemoveItem(CosmeticsController.CosmeticCategory.Fur, CosmeticsController.CosmeticSlots.Fur);
            if (prefab != null)
            {
                var parentAsset = prefab;
                try
                {
                    if (materialIndex == 0)
                    {
                        VRRig rig = GorillaTagger.Instance.offlineVRRig;
                        currentMaterial.mat = parentAsset.GetComponent<MeshRenderer>().material;
                        if (usingTextMethod)
                        {
                            currentMaterial.customColours = materialCustomColours;
                        }
                        else
                        {
                            currentMaterial.customColours = matDes.customColors;
                        }

                        if (currentMaterial.customColours)
                        {
                            currentMaterial.mat.color = rig.playerColor;
                        }
                        var table = PhotonNetwork.LocalPlayer.CustomProperties;
                        table["CustomMaterial"] = matDes.Name;
                        PhotonNetwork.LocalPlayer.SetCustomProperties(table);
                        rig.materialsToChangeTo[materialIndex] = currentMaterial.mat;
                        Material[] sharedMaterials = rig.mainSkin.sharedMaterials;
                        sharedMaterials[0] = rig.materialsToChangeTo[rig.setMatIndex];
                        sharedMaterials[1] = rig.defaultSkin.chestMaterial;
                        rig.mainSkin.sharedMaterials = sharedMaterials;
                    }
                    else if (materialIndex == 2)
                    {
                        currentTaggedMaterial.mat = parentAsset.GetComponent<MeshRenderer>().material;
                        if (usingTextMethod)
                        {
                            currentTaggedMaterial.customColours = materialCustomColours;
                        }
                        else
                        {
                            currentTaggedMaterial.customColours = matDes.customColors;
                        }
                        VRRig rig = GorillaTagger.Instance.offlineVRRig;
                        if (currentTaggedMaterial.customColours)
                        {
                            currentTaggedMaterial.mat.color = new Color(1f, 0.4f, 0f);
                        }
                        var table = PhotonNetwork.LocalPlayer.CustomProperties;
                        table["CustomTagMaterial"] = matDes.Name;
                        PhotonNetwork.LocalPlayer.SetCustomProperties(table);
                        rig.materialsToChangeTo[materialIndex] = currentTaggedMaterial.mat;
                        Material[] sharedMaterials = rig.mainSkin.sharedMaterials;
                        sharedMaterials[0] = rig.materialsToChangeTo[rig.setMatIndex];
                        sharedMaterials[1] = rig.defaultSkin.chestMaterial;
                        rig.mainSkin.sharedMaterials = sharedMaterials;
                    }
                    else
                    {
                        VRRig rig = GorillaTagger.Instance.offlineVRRig;
                        rig.materialsToChangeTo[materialIndex] = currentTaggedMaterial.mat;
                    }
                    Destroy(parentAsset);
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }
        }
        public string GetCosName(GameObject obj, string mode)
        {
            string cosname = "Empty";

            if (obj.TryGetComponent(out Text values))
            {
                usingTextMethod = true;
                string[] info = values.text.Split("$");
                switch (mode)
                {
                    case "Material":
                        cosname = info[0];
                        break;
                    case "Holdable":
                        cosname = info[0];
                        break;
                    case "Badge":
                        cosname = info[0];
                        break;
                    case "Hat":
                        cosname = info[0];
                        break;
                }
            }
            else
            {
                usingTextMethod = false;
                switch (mode)
                {
                    case "Material":
                        cosname = obj.GetComponent<MaterialDescriptor>().Name;
                        break;
                    case "Holdable":
                        cosname = obj.GetComponent<HoldableDescriptor>().Name;
                        break;
                    case "Hat":
                        cosname = obj.GetComponent<HatDescriptor>().Name;
                        break;
                    case "Badge":
                        cosname = obj.GetComponent<BadgeDescriptor>().Name;
                        break;
                }
            }
            return cosname;
        }

        public void EnableMaterial()
        {
            VRRig rig = GorillaTagger.Instance.offlineVRRig;
            currentMaterial.mat.color = rig.playerColor;
            rig.materialsToChangeTo[0] = currentMaterial.mat;
            Material[] sharedMaterials = rig.mainSkin.sharedMaterials;
            sharedMaterials[0] = rig.materialsToChangeTo[rig.setMatIndex];
            sharedMaterials[1] = rig.defaultSkin.chestMaterial;
            rig.mainSkin.sharedMaterials = sharedMaterials;
            if (currentTaggedMaterial.customColours && currentTaggedMaterial.mat != null)
            {
                currentTaggedMaterial.mat.color = new Color(1f, 0.4f, 0f);
            }
        }

        public void EnableNetworkMaterial(VRRig rig)
        {
            Photon.Realtime.Player p;
            if(cosmeticsplayers.TryGetValue(rig, out p))
            {
                if (p.CustomProperties.TryGetValue("CustomMaterial", out object mat))
                {
                    p.CustomProperties.TryGetValue("Colour", out object co);
                    Color col = parseColor(co.ToString());
                    LoadNetworkMaterial(mat.ToString(), 0, rig, p, col);
                }
                if (p.CustomProperties.TryGetValue("CustomTagMaterial", out object tagmat))
                {
                    p.CustomProperties.TryGetValue("Colour", out object color);
                    Color c = parseColor(color.ToString());
                    LoadNetworkMaterial(tagmat.ToString(), 2, rig, p, c);
                }
            }
        }

        public void UpdateColour(Color colour)
        {
            var table = PhotonNetwork.LocalPlayer.CustomProperties;
            table["Colour"] = colour.ToString();
            PhotonNetwork.LocalPlayer.SetCustomProperties(table);
            if (currentMaterial.mat != null)
            {
                VRRig rig = GorillaTagger.Instance.offlineVRRig;
                rig.materialsToChangeTo[0] = currentMaterial.mat;
                if (currentMaterial.customColours)
                {
                    currentMaterial.mat.color = colour;
                }
                Material[] sharedMaterials = rig.mainSkin.sharedMaterials;
                sharedMaterials[0] = rig.materialsToChangeTo[rig.setMatIndex];
                sharedMaterials[1] = rig.defaultSkin.chestMaterial;
                rig.mainSkin.sharedMaterials = sharedMaterials;
                
            }
        }

        public void GetInfo(string file, string mode)
        {
            GameObject cosmetic;
            string[] info;
            assetCache.TryGetValue(file, out cosmetic);

            if (cosmetic.TryGetComponent(out Text values))
            {
                usingTextMethod = true;
                info = values.text.Split("$");
                switch (mode)
                {
                    case "Material":
                        currentCosmeticFile = file;
                        cosmeticName = info[0];
                        cosmeticAuthor = info[1];
                        cosmeticDescription = info[2];
                        materialCustomColours = info[3].ToUpper() == "TRUE";
                        break;
                    case "Holdable":
                        currentCosmeticFile = file;
                        cosmeticName = info[0];
                        cosmeticAuthor = info[1];
                        cosmeticDescription = info[2];
                        leftHand = info[3].ToUpper() == "TRUE";
                        break;
                    case "Badge":
                        currentCosmeticFile = file;
                        cosmeticName = info[0];
                        cosmeticAuthor = info[1];
                        cosmeticDescription = info[2];
                        break;
                    case "Hat":
                        currentCosmeticFile = file;
                        cosmeticName = info[0];
                        cosmeticAuthor = info[1];
                        cosmeticDescription = info[2];
                        break;
                }
            }
            else
            {
                usingTextMethod = false;
                switch (mode)
                {
                    case "Material":
                        currentCosmeticFile = file;
                        matDes = cosmetic.GetComponent<MaterialDescriptor>();
                        break;
                    case "Holdable":
                        currentCosmeticFile = file;
                        holdableDes = cosmetic.GetComponent<HoldableDescriptor>();
                        break;
                    case "Hat":
                        currentCosmeticFile = file;
                        hatDes = cosmetic.GetComponent<HatDescriptor>();
                        break;
                    case "Badge":
                        currentCosmeticFile = file;
                        badgeDes = cosmetic.GetComponent<BadgeDescriptor>();
                        break;
                }
            }
        }
        Color parseColor(string sourceString)
        {
            if (sourceString == null || sourceString == string.Empty || sourceString == "$")
            {
                return Color.black;
            }
            string outString;
            Color outColor;
            string[] splitString;

            // Trim extranious parenthesis
            outString = sourceString.Replace("(", string.Empty);
            outString = outString.Replace(")", string.Empty);
            outString = outString.Replace("RGBA", string.Empty);

            // Split delimted values into an array
            splitString = outString.Split(",");

            // Build new Vector3 from array elements
            float x;
            float y;
            float z;
            float.TryParse(splitString[0], out x);
            float.TryParse(splitString[1], out y);
            float.TryParse(splitString[2], out z);
            outColor.r = x;
            outColor.g = y;
            outColor.b = z;
            outColor.a = 1f;

            return outColor;
        }
    }
    
    struct customMaterial
    {
        public Material mat;
        public bool customColours;
    }
}