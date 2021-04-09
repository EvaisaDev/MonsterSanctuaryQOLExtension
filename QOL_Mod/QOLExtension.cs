using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using MonoMod.Cil;
using Mono.Cecil.Cil;
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
        public const string ModVersion = "0.1.2";

        Dictionary<KeyCode, Monster> monster_shortcuts = new Dictionary<KeyCode, Monster>();
        Monster SelectedMonster;

        public static BaseAction skipTurnAction;

        private ConfigEntry<bool> SkillMenuAllMonsters;
        private ConfigEntry<bool> FollowerMonsterBinding;
        private ConfigEntry<bool> NewGamePlusExplore;
        //private ConfigEntry<bool> SkipTurnButton;

        public QOLExtension()
        {
            SkillMenuAllMonsters = Config.Bind("General", "SkillMenuAllMonsters", true, "Lets you switch through all of your monsters in the skill menu instead of only active ones.");
            FollowerMonsterBinding = Config.Bind("General", "FollowerMonsterBinding", true, "Lets you keybinds follower monsters by holding shift and pressing a button while hovering over a monster in the follower menu.");
            NewGamePlusExplore = Config.Bind("General", "NewGamePlusExplore", false, "Removes New Game+ limitations");
            //SkipTurnButton = Config.Bind("General", "SkipTurnButton", true, "Lets you skip a turn which will not be counted for the turn counter.");
            On.PlayerController.LoadGame += PlayerController_LoadGame;
            On.GameController.InitPlayerStartSetup += GameController_InitPlayerStartSetup;
            On.MonsterSelector.Update += MonsterSelector_Update;
            On.MonsterSelector.OnItemHovered += MonsterSelector_OnItemHovered;
            On.MonsterManager.GetNextMonster += MonsterManager_GetNextMonster;
            On.OptionsManager.GetCombatSpeedMultiplicator += OptionsManager_GetCombatSpeedMultiplicator;
            On.OptionsManager.ChangeCombatSpeed += OptionsManager_ChangeCombatSpeed;
          //  On.OptionsMenu.ShowGameplayOptions += OptionsMenu_ShowGameplayOptions;
            On.OptionsMenu.OnOptionsSelected += OptionsMenu_OnOptionsSelected;
            On.CombatController.Start += CombatController_Start;

            On.MonsterSelector.UpdateDisabledStatus += MonsterSelector_UpdateDisabledStatus;
            //On.CombatMenu.ShowActions += CombatMenu_ShowActions;
           // IL.CombatMenu.ShowActions += CombatMenu_ShowActions1;

            /*
            FindAllRootGameObjects().ToList().ForEach(item =>
            {
                Debug.Log(item);
            });
            */

           /* var skipTurnObject = new GameObject();

            skipTurnAction = skipTurnObject.AddComponent<BaseAction>();

            skipTurnAction.Name = "Skip Turn";
            skipTurnAction.Icon = "icon_weapon";
            skipTurnAction.IsDefaultAction = true;
            skipTurnAction.TargetType = ETargetType.Self;
            skipTurnAction.Tooltip = "Skip the turn.";
            skipTurnAction.Mana = 0;
            skipTurnAction.ID = -1;
           */
            // var listItem = GameObject.Instantiate(FindAllRootGameObjects().FirstOrDefault(item => item.gameObject.name == "CombatMenuItem"));


            //CombatController.Instance.combatUi.Menu.MenuList.AddMenuItem(listItem.GetComponent<MenuListItem>());

            /*
            self.AddOption("SkillMenuAllMonsters", "Skill Menu Inactive Monsters", self.GetBoolScring(SkillMenuAllMonsters.Value), false);
            self.AddOption("FollowerMonsterBinding", "Follower Keybinding", self.GetBoolScring(FollowerMonsterBinding.Value), false);
            self.AddOption("SkipTurnButton", "Skip Turn Button", self.GetBoolScring(SkipTurnButton.Value), false);
             */

            MonSancAPI.MonSancAPI.RegisterLanguageToken(new MonSancAPI.MonSancAPI.LanguageToken("QOLSkillMenuInactiveMonsters", "Skill Menu Inactive Monsters"));
            MonSancAPI.MonSancAPI.RegisterLanguageToken(new MonSancAPI.MonSancAPI.LanguageToken("FollowerMonsterBinding", "Follower Keybinding"));
            MonSancAPI.MonSancAPI.RegisterLanguageToken(new MonSancAPI.MonSancAPI.LanguageToken("SkipTurnButton", "Skip Turn Button"));
            MonSancAPI.MonSancAPI.RegisterLanguageToken(new MonSancAPI.MonSancAPI.LanguageToken("NewGamePlusExplore", "New Game+ Exploration Mode"));
            

            MonSancAPI.MonSancAPI.RegisterOption(MonSancAPI.MonSancAPI.optionType.gameplay, "QOLSkillMenuInactiveMonsters", delegate (OptionsMenu self) { return Utils.LOCA("QOLSkillMenuInactiveMonsters", ELoca.UI); }, delegate (OptionsMenu self) { return self.GetBoolScring(SkillMenuAllMonsters.Value); }, false, delegate (OptionsMenu self) { return false; });
            MonSancAPI.MonSancAPI.RegisterOption(MonSancAPI.MonSancAPI.optionType.gameplay, "FollowerMonsterBinding", delegate (OptionsMenu self) { return Utils.LOCA("FollowerMonsterBinding", ELoca.UI); }, delegate (OptionsMenu self) { return self.GetBoolScring(FollowerMonsterBinding.Value); }, false, delegate (OptionsMenu self) { return false; });
            MonSancAPI.MonSancAPI.RegisterOption(MonSancAPI.MonSancAPI.optionType.gameplay, "NewGamePlusExplore", delegate (OptionsMenu self) { return Utils.LOCA("NewGamePlusExplore", ELoca.UI); }, delegate (OptionsMenu self) { return self.GetBoolScring(NewGamePlusExplore.Value); }, false, delegate (OptionsMenu self) { return false; });
            //  MonSancAPI.MonSancAPI.RegisterOption(MonSancAPI.MonSancAPI.optionType.gameplay, "SkipTurnButton", delegate (OptionsMenu self) { return Utils.LOCA("SkipTurnButton", ELoca.UI); }, delegate (OptionsMenu self) { return self.GetBoolScring(SkipTurnButton.Value); }, false, delegate (OptionsMenu self) { return false; });
        }

        private void MonsterSelector_UpdateDisabledStatus(On.MonsterSelector.orig_UpdateDisabledStatus orig, MonsterSelector self, MonsterSelectorView monsterView)
        {
            if (NewGamePlusExplore.Value)
            {
                if (self.CurrentSelectType == MonsterSelector.MonsterSelectType.SelectItemTarget)
                {
                    monsterView.SetDisabled(!IngameMenuController.Instance.Inventory.CurrentSelectedItem.Consumable.CanBeUsedOnMonster(monsterView.Monster) || IngameMenuController.Instance.Inventory.CurrentSelectedItem.Quantity == 0);
                    return;
                }
                if (self.CurrentSelectType == MonsterSelector.MonsterSelectType.SelectLeaveAtFarm)
                {
                    monsterView.SetDisabled(!monsterView.Monster.CanGiveawayMonster(false) || PlayerController.Instance.Monsters.Inactive.Count == 0);
                    return;
                }
                monsterView.SetDisabled(false);
            }
            else
            {
                orig(self, monsterView);
            }
        }

        private void CombatMenu_ShowActions1(ILContext il)
        {
            var cursor = new ILCursor(il);
            cursor.GotoNext(MoveType.After, x => x.MatchCallvirt(typeof(MenuList), nameof(MenuList.Clear)));
            cursor.Index += 1;
            cursor.Emit(Mono.Cecil.Cil.OpCodes.Ldarg_0);
            cursor.Emit(Mono.Cecil.Cil.OpCodes.Ldarg_1);
            cursor.EmitDelegate<System.Action<CombatMenu, Monster>>(ActionsHook);
            // cursor.Emit(OpCodes.Ldloc_0);
            //   cursor.EmitDelegate<System.Action<WeightedSelection<DirectorCard>>>(AddObject);

            Debug.Log(il.ToString());
        }

        private void ActionsHook(CombatMenu self, Monster monster)
        {

            self.MenuList.MaxItemsPerList += 1;

            MenuListItem menuListItem = self.MenuList.AddDisplayable(skipTurnAction, -1, -1);
            //menuListItem.SetText("Skip Turn");
        }

        private void CombatMenu_ShowActions(On.CombatMenu.orig_ShowActions orig, CombatMenu self, Monster monster)
        {

            //var newAction = GameObject.Instantiate(GameController.Instance.Combat.DefaultActions[4]);
            //newAction.SetActive(false);
            //newAction.transform.SetParent(parent);
            //GameController.Instance.Combat.DefaultActions.Add(newAction);


            orig(self, monster);

            //menuListItem.GetComponent<CombatMenuItem>().SetAction(defaultAttackReplacement.GetComponent<BaseAction>(), true, monster);
        }

        private void CombatController_Start(On.CombatController.orig_Start orig, CombatController self)
        {
            orig(self);
           // var parent = self.DefaultActions[4].transform.parent.transform;
          //  var newAction = GameObject.Instantiate(self.DefaultActions[4]);
            //newAction.SetActive(false);
           // newAction.transform.SetParent(parent);
            //self.DefaultActions.Add(self.DefaultActions[4]);
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
           // Debug.Log(text);
            if(text == "QOLSkillMenuInactiveMonsters")
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
            else if (text == "NewGamePlusExplore")
            {
                if (NewGamePlusExplore.Value == true)
                {
                    NewGamePlusExplore.Value = false;
                }
                else
                {
                    NewGamePlusExplore.Value = true;
                }
                NewGamePlusExplore.ConfigFile.Save();
            }
            /* else if (text == "SkipTurnButton")
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
             }*/
            orig(self, menuItem);
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

        private void PlayerController_LoadGame(On.PlayerController.orig_LoadGame orig, PlayerController self, SaveGameData saveGameData, bool newGamePlusSetup)
        {
            monster_shortcuts = new Dictionary<KeyCode, Monster>();
            orig(self, saveGameData, newGamePlusSetup);
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
