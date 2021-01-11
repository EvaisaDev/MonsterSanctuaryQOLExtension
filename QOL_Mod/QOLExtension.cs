using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using System;
using System.Collections;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;
using Random = System.Random;
using System.IO;
using System.Reflection.Emit;
using MonSancAPI;
using System.Collections.Generic;

namespace QOLExtension
{
    [BepInDependency("evaisa.MonSancAPI")]
    [BepInPlugin(ModGUID, ModName, ModVersion)]
    public class QOLExtension : BaseUnityPlugin
    {
        public const string ModGUID = "evaisa.qolmod";
        public const string ModName = "QOLExtension";
        public const string ModVersion = "0.1.0";

        Dictionary<KeyCode, Monster> monster_shortcuts = new Dictionary<KeyCode, Monster>();
        Monster SelectedMonster;


        private ConfigEntry<bool> SkillMenuAllMonsters;
        private ConfigEntry<bool> FollowerMonsterBinding;
        private ConfigEntry<bool> SkipTurnButton;

        public QOLExtension()
        {
            SkillMenuAllMonsters = Config.Bind("General", "SkillMenuAllMonsters", true, "Lets you switch through all of your monsters in the skill menu instead of only active ones.");
            FollowerMonsterBinding = Config.Bind("General", "FollowerMonsterBinding", true, "Lets you keybinds follower monsters by holding shift and pressing a button while hovering over a monster in the follower menu.");
            SkipTurnButton = Config.Bind("General", "SkipTurnButton", true, "Lets you skip a turn which will not be counted for the turn counter.");
            On.PlayerController.LoadGame += PlayerController_LoadGame;
            On.GameController.InitPlayerStartSetup += GameController_InitPlayerStartSetup;
            On.MonsterSelector.Update += MonsterSelector_Update;
            On.MonsterSelector.OnItemHovered += MonsterSelector_OnItemHovered;
            On.MonsterManager.GetNextMonster += MonsterManager_GetNextMonster;
            On.OptionsManager.GetCombatSpeedMultiplicator += OptionsManager_GetCombatSpeedMultiplicator;
            On.OptionsManager.ChangeCombatSpeed += OptionsManager_ChangeCombatSpeed;
            On.OptionsMenu.ShowGameplayOptions += OptionsMenu_ShowGameplayOptions;
            On.OptionsMenu.OnOptionsSelected += OptionsMenu_OnOptionsSelected;

            /*
            FindAllRootGameObjects().ToList().ForEach(item =>
            {
                Debug.Log(item);
            });
            */

            var listItem = GameObject.Instantiate(FindAllRootGameObjects().FirstOrDefault(item => item.gameObject.name == "CombatMenuItem"));

            CombatController.Instance.combatUi.Menu.MenuList.AddMenuItem(listItem.GetComponent<MenuListItem>());
        }


        public static IEnumerable<GameObject> FindAllRootGameObjects()
        {
            return Resources.FindObjectsOfTypeAll<Transform>()
                .Where(t => t.parent == null)
                .Select(x => x.gameObject);
        }

        private void OptionsMenu_OnOptionsSelected(On.OptionsMenu.orig_OnOptionsSelected orig, OptionsMenu self, MenuListItem menuItem)
        {
            string text = self.optionNames[self.GetCurrentOptionIndex()];
            Debug.Log(text);
            if(text == "SkillMenuAllMonsters")
            {
                if(SkillMenuAllMonsters.Value == true)
                {
                    SkillMenuAllMonsters.Value = false;
                }
                else
                {
                    SkillMenuAllMonsters.Value = true;
                }
                SkillMenuAllMonsters.ConfigFile.Save();
            }
            else if (text == "FollowerMonsterBinding")
            {
                if (FollowerMonsterBinding.Value == true)
                {
                    FollowerMonsterBinding.Value = false;
                }
                else
                {
                    FollowerMonsterBinding.Value = true;
                }
                FollowerMonsterBinding.ConfigFile.Save();
            }
            else if (text == "SkipTurnButton")
            {
                if (SkipTurnButton.Value == true)
                {
                    SkipTurnButton.Value = false;
                }
                else
                {
                    SkipTurnButton.Value = true;
                }
                SkipTurnButton.ConfigFile.Save();
            }
            orig(self, menuItem);
        }

        private void OptionsMenu_ShowGameplayOptions(On.OptionsMenu.orig_ShowGameplayOptions orig, OptionsMenu self)
        {
            orig(self);
            self.AddOption("SkillMenuAllMonsters", "Skill Menu Inactive Monsters", self.GetBoolScring(SkillMenuAllMonsters.Value), false);
            self.AddOption("FollowerMonsterBinding", "Follower Keybinding", self.GetBoolScring(FollowerMonsterBinding.Value), false);
            self.AddOption("SkipTurnButton", "Skip Turn Button", self.GetBoolScring(SkipTurnButton.Value), false);
            self.Captions.UpdateItemPositions(false);
            self.BaseOptions.UpdateItemPositions(false);
        }

        private void OptionsManager_ChangeCombatSpeed(On.OptionsManager.orig_ChangeCombatSpeed orig, OptionsManager self, int direction)
        {
            self.OptionsData.CombatSpeed += direction;
            if (self.OptionsData.CombatSpeed >7)
            {
                self.OptionsData.CombatSpeed = 0;
                return;
            }
            if (self.OptionsData.CombatSpeed < 0)
            {
                self.OptionsData.CombatSpeed = 7;
            }
        }



        private float OptionsManager_GetCombatSpeedMultiplicator(On.OptionsManager.orig_GetCombatSpeedMultiplicator orig, OptionsManager self)
        {
            switch (self.OptionsData.CombatSpeed)
            {
                case 0:
                    return 1f;
                case 1:
                    return 1.25f;
                case 2:
                    return 1.5f;
                case 3:
                    return 1.75f;
                case 4:
                    return 2f;
                case 5:
                    return 5f;
                case 6:
                    return 10f;
                case 7:
                    return 20f;
                default:
                    return 1f;
            }
        }

        private Monster MonsterManager_GetNextMonster(On.MonsterManager.orig_GetNextMonster orig, MonsterManager self, Monster current, int dir)
        {
            if (SkillMenuAllMonsters.Value)
            {
                int num = self.Active.Concat(self.Inactive).ToList().IndexOf(current) + dir;
                if (num < 0)
                {
                    num = self.Active.Concat(self.Inactive).ToList().Count - 1;
                }
                if (num >= self.Active.Concat(self.Inactive).ToList().Count)
                {
                    num = 0;
                }
                return self.Active.Concat(self.Inactive).ToList()[num];
            }
            else
            {
                return orig(self, current, dir);
            }
        }

        void Update()
        {
            if (FollowerMonsterBinding.Value)
            {
                foreach (KeyValuePair<KeyCode, Monster> pair in monster_shortcuts)
                {
                    if (Input.GetKeyDown(pair.Key))
                    {
                        if (PlayerController.Instance != null && PlayerController.Instance.Follower != null)
                        {
                            PlayerController.Instance.Follower.SwitchMonster(pair.Value);
                        }
                    }
                }
            }
        }



        private void MonsterSelector_OnItemHovered(On.MonsterSelector.orig_OnItemHovered orig, MonsterSelector self, MenuListItem item)
        {
            SelectedMonster = item.GetComponent<MonsterSelectorView>().Monster;
            orig(self, item);
        }

        private void GameController_InitPlayerStartSetup(On.GameController.orig_InitPlayerStartSetup orig, GameController self)
        {
            monster_shortcuts = new Dictionary<KeyCode, Monster>();
            orig(self);
        }

        private void PlayerController_LoadGame(On.PlayerController.orig_LoadGame orig, PlayerController self, SaveGameData saveGameData)
        {
            monster_shortcuts = new Dictionary<KeyCode, Monster>();
            orig(self, saveGameData);
        }


        private void MonsterSelector_Update(On.MonsterSelector.orig_Update orig, MonsterSelector self)
        {
            orig(self);
            if (FollowerMonsterBinding.Value)
            {
                if (self.CurrentSelectType == MonsterSelector.MonsterSelectType.SelectFollower)
                {
                    foreach (KeyCode vKey in System.Enum.GetValues(typeof(KeyCode)))
                    {
                        if (Input.GetKeyDown(vKey) && Input.GetKey(KeyCode.LeftShift) || Input.GetKeyDown(vKey) && Input.GetKey(KeyCode.RightShift))
                        {
                            if (monster_shortcuts.ContainsKey(vKey))
                            {
                                monster_shortcuts[vKey] = SelectedMonster;
                            }
                            else
                            {
                                monster_shortcuts.Add(vKey, SelectedMonster);
                            }
                        }
                    }
                }
            }
        }
    }
}
