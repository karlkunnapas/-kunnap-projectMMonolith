namespace App.Resources.Views.Company.Promotion {
    using System;

    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "4.0.0.0")]
    [System.Diagnostics.DebuggerNonUserCodeAttribute()]
    [System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    public class Edit {
        private static System.Resources.ResourceManager resourceMan;
        private static System.Globalization.CultureInfo resourceCulture;

        internal Edit() {
        }

        public static System.Resources.ResourceManager ResourceManager {
            get {
                if (object.Equals(null, resourceMan)) {
                    var temp = new System.Resources.ResourceManager("WebAppClient.App.Resources.Views.Company.Promotion.Edit", typeof(Edit).Assembly);
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
        public static string CodeLabel => ResourceManager.GetString("CodeLabel", resourceCulture);
        public static string DiscountLabel => ResourceManager.GetString("DiscountLabel", resourceCulture);
        public static string ValidFromLabel => ResourceManager.GetString("ValidFromLabel", resourceCulture);
        public static string ValidToLabel => ResourceManager.GetString("ValidToLabel", resourceCulture);
        public static string IsActiveLabel => ResourceManager.GetString("IsActiveLabel", resourceCulture);
        public static string SaveAction => ResourceManager.GetString("SaveAction", resourceCulture);
        public static string BackAction => ResourceManager.GetString("BackAction", resourceCulture);
    }
}
