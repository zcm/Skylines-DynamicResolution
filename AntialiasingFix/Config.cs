using System.IO;
using System.Xml.Serialization;

namespace DynamicResolution
{

    public class Configuration
    {
        public static readonly string DEFAULT_CONFIG_PATH = "DynamicResolutionConfig.xml";

        public float ssaaFactor = 1.0f;
        public bool unlockSlider = false;
        public bool ssaoState = true;
        public bool lowerVRAMUsage = false;
        public int sliderMaximumIndex = 0;

        public void OnPreSerialize()
        {
            
        }

        public void OnPostDeserialize()
        {
            if (ssaaFactor > 4.0f && sliderMaximumIndex < 2)
            {
                sliderMaximumIndex = 2;
            }
            else if (ssaaFactor > 3.0f && sliderMaximumIndex < 1)
            {
                sliderMaximumIndex = 1;
            }
        }

        public static void Serialize(string filename, Configuration config)
        {
            var serializer = new XmlSerializer(typeof(Configuration));

            using (var writer = new StreamWriter(filename))
            {
                config.OnPreSerialize();
                serializer.Serialize(writer, config);
            }
        }

        public static Configuration Deserialize(string filename)
        {
            var serializer = new XmlSerializer(typeof(Configuration));

            try
            {
                using (var reader = new StreamReader(filename))
                {
                    var config = (Configuration)serializer.Deserialize(reader);
                    config.OnPostDeserialize();
                    return config;
                }
            }
            catch { }

            return null;
        }
    }

}
