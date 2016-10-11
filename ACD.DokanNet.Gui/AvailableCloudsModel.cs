namespace Azi.Cloud.DokanNet.Gui
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using Common;
    using Tools;

    public class AvailableCloudsModel
    {
        public AvailableCloudsModel()
        {
            AvailableClouds = new List<AvailableCloud>();
            foreach (var file in Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory, "Clouds.*.dll"))
            {
                try
                {
                    var assembly = Assembly.LoadFrom(file);

                    var types = assembly.GetExportedTypes().Where(t => typeof(IHttpCloud).IsAssignableFrom(t));

                    AvailableClouds.AddRange(types.Where(t => t.IsClass)
                            .Select(t => new AvailableCloud
                            {
                                AssemblyFileName = Path.GetFileName(file),
                                ClassName = t.FullName,
                                Name = (string)t.GetProperty("CloudServiceName").GetValue(null),
                                Icon = (string)t.GetProperty("CloudServiceIcon").GetValue(null)
                            }));
                }
                catch (Exception ex)
                {
                    Log.Error(ex);
                }
            }
        }

        public List<AvailableCloud> AvailableClouds { get; }

        public class AvailableCloud
        {
            public string ClassName { get; set; }

            public string Name { get; set; }

            public string Icon { get; set; }

            public string AssemblyFileName { get; set; }
        }
    }
}