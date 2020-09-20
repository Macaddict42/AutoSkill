using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Timers;
using ExileCore;
using ExileCore.PoEMemory;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.MemoryObjects;
using SharpDX;
using Timer = System.Timers.Timer;

namespace AutoSkill
{
    public class AutoSkill : BaseSettingsPlugin<AutoSkillSettings>
    {
        private bool IsTownOrHideout => GameController.Area.CurrentArea.IsTown || GameController.Area.CurrentArea.IsHideout;
        private readonly HashSet<Entity> nearbyMonsters = new HashSet<Entity>();
        private Timer skillTimer;
        private Stopwatch settingsStopwatch;
        private Stopwatch intervalStopwatch;
        private KeyboardHelper keyboard;
        private int highlightSkill;

        public AutoSkill()
        {
            Name = "AutoSkill";
        }

        public override bool Initialise()
        {
            OnSettingsToggle();
            Settings.Enable.OnValueChanged += Enable_OnValueChanged;
            Settings.ConnectedSkill.OnValueChanged += ConnectedSkill_OnValueChanged; ;
            settingsStopwatch = new Stopwatch();
            intervalStopwatch = Stopwatch.StartNew();
            skillTimer = new Timer(100) {AutoReset = true};
            skillTimer.Elapsed += SkillTimerOnElapsed;
            skillTimer.Start();
            keyboard = new KeyboardHelper(GameController);
            return true;
        }

        private void ConnectedSkill_OnValueChanged(object sender, int e)
        {
            ConnectedSkillOnOnValueChanged();
        }

        private void Enable_OnValueChanged(object sender, bool e)
        {
            OnSettingsToggle();
        }

        private bool ChatOpen
        {
            get
            {
                var chatBox = GameController?.Game?.IngameState?.UIRoot.GetChildAtIndex(1)?.GetChildAtIndex(106);
                if (chatBox == null)
                    return false;
                return chatBox.ChildCount == 5;
            }
        }

        private void SkillTimerOnElapsed(object sender, ElapsedEventArgs e)
        {
            if (Settings.Enable.Value)
            {
                SkillMain();
            }
        }

        private void ConnectedSkillOnOnValueChanged()
        {
            highlightSkill = Settings.ConnectedSkill.Value - 1;
            settingsStopwatch.Restart();
        }

        private void OnSettingsToggle()
        {
            try
            {
                if (Settings.Enable.Value)
                {
                    GameController.Area.RefreshState();

                    settingsStopwatch.Reset();
                    skillTimer.Start();
                }
                else
                {
                    settingsStopwatch.Stop();
                    skillTimer.Stop();

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

            if (settingsStopwatch.IsRunning && settingsStopwatch.ElapsedMilliseconds < 1200)
            {
                if (highlightSkill == -1)
                {
                    var pos = GameController.Game.IngameState.Data.LocalPlayer.GetComponent<Render>().Pos;
                    DrawEllipseToWorld(pos, Settings.NearbyMonster.Value, 50, 2, Color.Yellow);
                }
                else
                {
                    IngameUIElements ingameUiElements = GameController.Game.IngameState.IngameUi;
                    Graphics.DrawFrame(ingameUiElements.SkillBar[highlightSkill].GetClientRect(), Color.Yellow, 3);
                }
            }
            else
            {
                settingsStopwatch.Stop();
            }
        }

        public override void EntityAdded(Entity entity)
        {
            if (!Settings.Enable.Value)
                return;

            if (entity.IsAlive && entity.IsHostile && entity.HasComponent<Monster>())
            {
                entity.GetComponent<Positioned>();
                nearbyMonsters.Add(entity);
            }
        }

        public override void EntityRemoved(Entity entity)
        {
            if (!Settings.Enable.Value)
                return;

            nearbyMonsters.Remove(entity);
        }

        private void SkillMain()
        {
            if (GameController == null || GameController.Window == null || GameController.Game.IngameState.Data.LocalPlayer == null || GameController.Game.IngameState.Data.LocalPlayer.Address == 0x00)
                return;

            if (!GameController.Window.IsForeground())
                return;

            if (!GameController.Game.IngameState.Data.LocalPlayer.IsValid)
                return;

            var playerLife = GameController.Game.IngameState.Data.LocalPlayer.GetComponent<Life>();
            if (playerLife == null || IsTownOrHideout || IsChatOpen)
                return;

            try
            { 
                if (ShouldUseSkill())
                {
                    var actorSkills = GameController.Game.IngameState.Data.LocalPlayer.GetComponent<Actor>().ActorSkills;
                    var actorSkill = actorSkills.FirstOrDefault(CanUseSkill);
                    if (actorSkill != null)
                    {
                        keyboard.KeyPressRelease(Settings.SkillKeyPressed.Value);
                        intervalStopwatch.Restart();
                    }
                }
            }
            catch (Exception ex)
            {
                LogError(ex.Message, 3);
            }
        }

        private bool ShouldUseSkill()
        {
            if (Settings.ThrottleFrequency && intervalStopwatch.ElapsedMilliseconds < Settings.Frequency)
                return false;
            if (Settings.CheckNearbyMonsters && !EnoughMonstersInRange())
                return false;
            if (Settings.UseBelowHpPercent && (GameController.Game.IngameState.Data.LocalPlayer.GetComponent<Life>().HPPercentage * 100) > Settings.HpPercentage)
                return false;
            return true;
        }

        private bool CanUseSkill(ActorSkill skill)
        {
            if (ChatOpen)
                return false;

            if (!skill.CanBeUsed || !skill.SkillSlotIndex.Equals(Settings.ConnectedSkill.Value - 1))
                return false;

            // Skip using phase run when we already have the buff
            if (skill.Name.Equals("NewPhaseRun"))
            {
                var buffs = GameController.Game.IngameState.Data.LocalPlayer.GetComponent<Life>().Buffs;
                if (buffs.Any(x => x.Name.Equals("new_phase_run")))
                    return false;
            }
            
            return true;
        }

        private bool IsChatOpen
        {
            get
            {
                if (!(GameController?.InGame ?? false))
                    return false;
                var chatBox = GetRootElement(UI_ELEMENT.CHAT);
                if (chatBox == null)
                    return false;
                return chatBox.ChildCount == 5;
            }
        }

        private Element GetRootElement(UI_ELEMENT element)
        {
            return GameController?.Game?.IngameState?.UIRoot.GetChildAtIndex(1)?.GetChildAtIndex((int)element);
        }

        private enum UI_ELEMENT
        {
            CHAT = 110,
        }

        private bool EnoughMonstersInRange()
        {
            Vector3 positionPlayer = GameController.Game.IngameState.Data.LocalPlayer.GetComponent<Render>().Pos;

            int monstersInRange = 0;
            foreach (Entity monster in new List<Entity>(nearbyMonsters))
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
                    var pointEnd1 = camera.WorldToScreen(plottedCirclePoints.Last());
                    var pointEnd2 = camera.WorldToScreen(plottedCirclePoints[0]);
                    Graphics.DrawLine(pointEnd1, pointEnd2, lineWidth, color);
                    return;
                }

                var point1 = camera.WorldToScreen(plottedCirclePoints[i]);
                var point2 = camera.WorldToScreen(plottedCirclePoints[i + 1]);
                Graphics.DrawLine(point1, point2, lineWidth, color);
            }
        }
    }
}
