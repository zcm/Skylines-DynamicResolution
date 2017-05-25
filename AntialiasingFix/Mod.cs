using ColossalFramework.UI;
using ICities;
using UnityEngine;

namespace DynamicResolution
{

    public class Mod : IUserMod
    {

        public string Name
        {
            get { return "Dynamic Resolution"; }
        }

        public string Description
        {
            get { return "Better antialiasing. Upsample/downsample from any resolution."; }
        }

        private Configuration config;
        private UIDropDown sliderMaxDropDown;

        public void OnSettingsUI(UIHelperBase helperBase)
        {
            config = Configuration.Deserialize(Configuration.DEFAULT_CONFIG_PATH);

            UIHelper generalHelper = (UIHelper)helperBase.AddGroup("Dynamic Resolution Settings");
            UIPanel generalPanel = (UIPanel)generalHelper.self;
            UILabel resetLabel = generalPanel.AddUIComponent<UILabel>();
            resetLabel.name = "DR_ResetLabel";
            resetLabel.text = "If the slider is stuck too high, or the game won't start, try resetting to the defaults.";
            generalHelper.AddButton("Reset to default settings", ResetToDefaultSettings);
            generalHelper.AddSpace(20);

            sliderMaxDropDown = (UIDropDown)generalHelper.AddDropdown("Slider Maximum", new string[] { "300% (default)", "400% (~GTX 880)", "500% (~GTX 980 Ti)" }, -1, OnSliderMaximumChanged);
            sliderMaxDropDown.width = 300;
            sliderMaxDropDown.selectedIndex = config.sliderMaximumIndex;
            UILabel sliderMaxLabel1 = generalPanel.AddUIComponent<UILabel>();
            sliderMaxLabel1.name = "DR_MaxSliderLabel1";
            UILabel sliderMaxLabel2 = generalPanel.AddUIComponent<UILabel>();
            sliderMaxLabel2.name = "DR_MaxSliderLabel2";
            sliderMaxLabel1.text = "Settings above 300% may glitch or break your game.";
            sliderMaxLabel2.text = "Reset to the default settings if you're having trouble.";
        }

        private void OnBeforeConfigurationChanged()
        {
            if (ModLoad.hook != null)
            {
                config = ModLoad.hook.config;
            }
        }

        private void ResetToDefaultSettings()
        {
            OnBeforeConfigurationChanged();

            sliderMaxDropDown.selectedIndex = 0;

            config.ssaaFactor = 1.0f;
            config.unlockSlider = false;
            config.ssaoState = true;
            config.lowerVRAMUsage = false;
            config.sliderMaximumIndex = 0;

            SaveConfig();

            if (ModLoad.hook != null)
            {
                ModLoad.hook.SetSSAAFactor(config.ssaaFactor, config.lowerVRAMUsage);
                ModLoad.hook.userSSAAFactor = config.ssaaFactor;
            }
        }

        private void OnSliderMaximumChanged(int option)
        {
            OnBeforeConfigurationChanged();

            config.sliderMaximumIndex = option;

            if (config.ssaaFactor > 4.0f && config.sliderMaximumIndex < 2)
            {
                config.ssaaFactor = 4.0f;
            }
            else if (config.ssaaFactor > 3.0f && config.sliderMaximumIndex < 1)
            {
                config.ssaaFactor = 3.0f;
            }

            SaveConfig();
        }

        private void SaveConfig()
        {
            Configuration.Serialize(Configuration.DEFAULT_CONFIG_PATH, config);
            RefreshCameraHook();
        }

        private void RefreshCameraHook()
        {
            if (ModLoad.hook != null)
            {
                ModLoad.hook.config = config;
            }
        }
    }

    public class ModLoad : LoadingExtensionBase
    {

        public static CameraHook hook = null;

        public override void OnLevelLoaded(LoadMode mode)
        {
            if (mode != LoadMode.NewGame && mode != LoadMode.LoadGame)
            {
                return;
            }

            var cameraController = GameObject.FindObjectOfType<CameraController>();
            hook = cameraController.gameObject.AddComponent<CameraHook>();
        }

        public override void OnLevelUnloading()
        {
            GameObject.Destroy(hook);
            hook = null;
        }
    }

}
