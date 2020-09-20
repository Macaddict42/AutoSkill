using ExileCore.Shared.Interfaces;
using ExileCore.Shared.Nodes;
using ExileCore.Shared.Attributes;
using System.Windows.Forms;

namespace AutoSkill
{
    public class AutoSkillSettings : ISettings
    {
        public AutoSkillSettings()
        {
            //plugin itself
            Enable = new ToggleNode(false);
            
            SkillKeyPressed = Keys.W;
            SkillSettings = new EmptyNode();
            CheckNearbyMonsters = new ToggleNode(false);
            UseBelowHpPercent = new ToggleNode(false);
            ThrottleFrequency = new ToggleNode(false);
            ConnectedSkill = new RangeNode<int>(1, 1, 8);
            NearbyMonster = new RangeNode<int>(5, 1, 100);
            NearbyMonsterRange = new RangeNode<int>(400, 1, 3000);
            HpPercentage = new RangeNode<int>(10, 0, 100);
            Frequency = new RangeNode<int>(4000, 0, 10000);
        }

        //Menu
        [Menu("Skill Settings", 1)]
        public EmptyNode SkillSettings { get; set; }
        [Menu("Skill Hotkey", "Set the hotkey of the skill", 10, 1)]
        public HotkeyNode SkillKeyPressed { get; set; }
        [Menu("Connected Skill", "Set the skill slot (1 = top left, 8 = bottom right)", 11, 1)]
        public RangeNode<int> ConnectedSkill { get; set; }

        [Menu("Check Nearby Monsters", 2)]
        public ToggleNode CheckNearbyMonsters { get; set; }
        [Menu("Nearby Monster Range", 12, 2)]
        public RangeNode<int> NearbyMonsterRange { get; set; }
        [Menu("Nearby Monsters", 13, 2)]
        public RangeNode<int> NearbyMonster { get; set; }

        [Menu("Use When HP Is Below %", 3)]
        public ToggleNode UseBelowHpPercent { get; set; }
        [Menu("Frequency", 14, 3)]
        public RangeNode<int> HpPercentage { get; set; }

        [Menu("Throttle Frequency", 4)]
        public ToggleNode ThrottleFrequency { get; set; }
        [Menu("Frequency", 15, 4)]
        public RangeNode<int> Frequency { get; set; }
        public ToggleNode Enable { get; set; }
    }
}
