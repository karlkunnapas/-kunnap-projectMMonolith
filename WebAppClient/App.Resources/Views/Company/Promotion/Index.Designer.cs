namespace App.Resources.Views.Company.Promotion {
    using System;

    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "4.0.0.0")]
    [System.Diagnostics.DebuggerNonUserCodeAttribute()]
    [System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    public class Index {
        private static System.Resources.ResourceManager resourceMan;
        private static System.Globalization.CultureInfo resourceCulture;

        internal Index() {
        }

        public static System.Resources.ResourceManager ResourceManager {
            get {
                if (object.Equals(null, resourceMan)) {
                    var temp = new System.Resources.ResourceManager("WebAppClient.App.Resources.Views.Company.Promotion.Index", typeof(Index).Assembly);
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
        public static string CreateAction => ResourceManager.GetString("CreateAction", resourceCulture);
        public static string NoPromotions => ResourceManager.GetString("NoPromotions", resourceCulture);
        public static string DiscountLabel => ResourceManager.GetString("DiscountLabel", resourceCulture);
        public static string ValidWindowLabel => ResourceManager.GetString("ValidWindowLabel", resourceCulture);
        public static string ActiveStatus => ResourceManager.GetString("ActiveStatus", resourceCulture);
        public static string InactiveStatus => ResourceManager.GetString("InactiveStatus", resourceCulture);
        public static string EditAction => ResourceManager.GetString("EditAction", resourceCulture);
        public static string DeleteAction => ResourceManager.GetString("DeleteAction", resourceCulture);
    }
}
