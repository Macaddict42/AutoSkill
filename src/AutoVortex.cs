﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Timers;
using PoeHUD.Controllers;
using PoeHUD.Models;
using PoeHUD.Plugins;
using PoeHUD.Poe.Components;
using PoeHUD.Poe.RemoteMemoryObjects;
using SharpDX;
using Timer = System.Timers.Timer;

namespace AutoVortex
{
    public class AutoVortex : BaseSettingsPlugin<AutoVortexSettings>
    {
        private bool isTownOrHideout;
        private readonly HashSet<EntityWrapper> nearbyMonsters = new HashSet<EntityWrapper>();
        private Timer vortexTimer;
        private Stopwatch stopwatch;
        private KeyboardHelper keyboard;
        private int highlightSkill;

        public override void Initialise()
        {
            PluginName = "Auto Vortex";
            
            OnSettingsToggle();
            Settings.Enable.OnValueChanged += OnSettingsToggle;
            Settings.VortexConnectedSkill.OnValueChanged += VortexConnectedSkillOnOnValueChanged;

            stopwatch = new Stopwatch();
            vortexTimer = new Timer(200) {AutoReset = true};
            vortexTimer.Elapsed += VortexTimerOnElapsed;
            vortexTimer.Start();
            keyboard = new KeyboardHelper(GameController);
        }

        private void VortexTimerOnElapsed(object sender, ElapsedEventArgs e)
        {
            if (Settings.Enable.Value)
            {
                VortexMain();
            }
        }

        private void VortexConnectedSkillOnOnValueChanged()
        {
            highlightSkill = Settings.VortexConnectedSkill.Value - 1;
            stopwatch.Restart();
        }

        private void OnSettingsToggle()
        {
            try
            {
                if (Settings.Enable.Value)
                {
                    GameController.Area.OnAreaChange += AreaOnOnAreaChange;
                    GameController.Area.RefreshState();

                    isTownOrHideout = GameController.Area.CurrentArea.IsTown;

                    stopwatch.Reset();
                    vortexTimer.Start();
                }
                else
                {
                    GameController.Area.OnAreaChange -= AreaOnOnAreaChange;

                    stopwatch.Stop();
                    vortexTimer.Stop();

                    nearbyMonsters.Clear();
                }
            }
            catch (Exception)
            {
            }
        }

        public override void Render()
        {
            base.Render();
            if (!Settings.Enable.Value) return;

            if (stopwatch.IsRunning && stopwatch.ElapsedMilliseconds < 1200)
            {
                if (highlightSkill == -1)
                {
                    var pos = GameController.Game.IngameState.Data.LocalPlayer.GetComponent<Render>().Pos;
                    DrawEllipseToWorld(pos, Settings.NearbyMonster.Value, 50, 2, Color.Yellow);
                }
                else
                {
                    IngameUIElements ingameUiElements = GameController.Game.IngameState.IngameUi;
                    Graphics.DrawFrame(ingameUiElements.SkillBar[highlightSkill].GetClientRect(), 3f, Color.Yellow);
                }
            }
            else
            {
                stopwatch.Stop();
            }
        }

        private void AreaOnOnAreaChange(AreaController area)
        {
            if (Settings.Enable.Value)
            {
                isTownOrHideout = area.CurrentArea.IsTown || area.CurrentArea.IsHideout;
            }
        }

        public override void EntityAdded(EntityWrapper entity)
        {
            if (!Settings.Enable.Value)
                return;

            if (entity.IsAlive && entity.IsHostile && entity.HasComponent<Monster>())
            {
                entity.GetComponent<Positioned>();
                nearbyMonsters.Add(entity);
            }
        }

        public override void EntityRemoved(EntityWrapper entity)
        {
            if (!Settings.Enable.Value)
                return;

            nearbyMonsters.Remove(entity);
        }

        private void VortexMain()
        {
            if (GameController == null || GameController.Window == null || GameController.Game.IngameState.Data.LocalPlayer == null || GameController.Game.IngameState.Data.LocalPlayer.Address == 0x00)
                return;

            if (!GameController.Window.IsForeground())
                return;

            if (!GameController.Game.IngameState.Data.LocalPlayer.IsValid)
                return;

            var playerLife = GameController.Game.IngameState.Data.LocalPlayer.GetComponent<Life>();
            if (playerLife == null || isTownOrHideout)
                return;

            try
            { 

                if (EnoughMonstersInRange())
                {
                    List<ActorSkill> actorSkills = GameController.Game.IngameState.Data.LocalPlayer.GetComponent<Actor>().ActorSkills;
                    ActorSkill skillVortex = actorSkills.FirstOrDefault(x => x.Name == "FrostBoltNova" && x.CanBeUsed && x.SkillSlotIndex.Equals(Settings.VortexConnectedSkill.Value - 1));
                    if (skillVortex != null)
                    {
                        keyboard.KeyPressRelease(Settings.VortexKeyPressed.Value);
                    }
                }
            }
            catch (Exception ex)
            {
                LogError(ex.Message, 3);
            }
        }

        private bool EnoughMonstersInRange()
        {
            Vector3 positionPlayer = GameController.Game.IngameState.Data.LocalPlayer.GetComponent<Render>().Pos;

            int monstersInRange = 0;
            foreach (EntityWrapper monster in new List<EntityWrapper>(nearbyMonsters))
            {
                if (monster.IsValid && monster.IsAlive && !monster.Path.Contains("ElementalSummoned"))
                {
                    Render positionMonster = monster.GetComponent<Render>();
                    int distance = (int)Math.Sqrt(Math.Pow(positionPlayer.X - positionMonster.X, 2.0) + Math.Pow(positionPlayer.Y - positionMonster.Y, 2.0));
                    if (distance <= Settings.NearbyMonsterRange.Value)
                        monstersInRange++;

                    if (monstersInRange >= Settings.NearbyMonster.Value)
                        return true;
                }
            }

            return false;
        }

        private void DrawEllipseToWorld(Vector3 vector3Pos, int radius, int points, int lineWidth, Color color)
        {
            var camera = GameController.Game.IngameState.Camera;

            var plottedCirclePoints = new List<Vector3>();
            var slice = 2 * Math.PI / points;
            for (var i = 0; i < points; i++)
            {
                var angle = slice * i;
                var x = (decimal)vector3Pos.X + decimal.Multiply((decimal)radius, (decimal)Math.Cos(angle));
                var y = (decimal)vector3Pos.Y + decimal.Multiply((decimal)radius, (decimal)Math.Sin(angle));
                plottedCirclePoints.Add(new Vector3((float)x, (float)y, vector3Pos.Z));
            }

            var rndEntity = GameController.Entities.FirstOrDefault(x =>
                x.HasComponent<Render>() && GameController.Player.Address != x.Address);

            for (var i = 0; i < plottedCirclePoints.Count; i++)
            {
                if (i >= plottedCirclePoints.Count - 1)
                {
                    var pointEnd1 = camera.WorldToScreen(plottedCirclePoints.Last(), rndEntity);
                    var pointEnd2 = camera.WorldToScreen(plottedCirclePoints[0], rndEntity);
                    Graphics.DrawLine(pointEnd1, pointEnd2, lineWidth, color);
                    return;
                }

                var point1 = camera.WorldToScreen(plottedCirclePoints[i], rndEntity);
                var point2 = camera.WorldToScreen(plottedCirclePoints[i + 1], rndEntity);
                Graphics.DrawLine(point1, point2, lineWidth, color);
            }
        }
    }
}
