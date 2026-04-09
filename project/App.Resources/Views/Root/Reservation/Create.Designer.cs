namespace App.Resources.Views.Root.Reservation {
    using System;

    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "4.0.0.0")]
    [System.Diagnostics.DebuggerNonUserCodeAttribute()]
    [System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    public class Create {
        private static System.Resources.ResourceManager resourceMan;
        private static System.Globalization.CultureInfo resourceCulture;

        internal Create() {
        }

        public static System.Resources.ResourceManager ResourceManager {
            get {
                if (object.Equals(null, resourceMan)) {
                    var temp = new System.Resources.ResourceManager("App.Resources.Views.Root.Reservation.Create", typeof(Create).Assembly);
                    resourceMan = temp;
                }
                return resourceMan;
            }
        }

        public static System.Globalization.CultureInfo Culture {
            get => resourceCulture;
            set => resourceCulture = value;
        }

        public static string Title => ResourceManager.GetString("Title", resourceCulture);
        public static string Heading => ResourceManager.GetString("Heading", resourceCulture);
        public static string StartTime => ResourceManager.GetString("StartTime", resourceCulture);
        public static string EndTime => ResourceManager.GetString("EndTime", resourceCulture);
        public static string EstimatedEnergy => ResourceManager.GetString("EstimatedEnergy", resourceCulture);
        public static string EstimatedCost => ResourceManager.GetString("EstimatedCost", resourceCulture);
        public static string PromotionCode => ResourceManager.GetString("PromotionCode", resourceCulture);
        public static string PromotionNoneOption => ResourceManager.GetString("PromotionNoneOption", resourceCulture);
        public static string Confirm => ResourceManager.GetString("Confirm", resourceCulture);
        public static string Back => ResourceManager.GetString("Back", resourceCulture);
    }
}
