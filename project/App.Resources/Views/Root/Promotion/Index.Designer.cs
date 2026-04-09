namespace App.Resources.Views.Root.Promotion {
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
                    var temp = new System.Resources.ResourceManager("App.Resources.Views.Root.Promotion.Index", typeof(Index).Assembly);
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
        public static string RedeemCodeLabel => ResourceManager.GetString("RedeemCodeLabel", resourceCulture);
        public static string RedeemAction => ResourceManager.GetString("RedeemAction", resourceCulture);
        public static string NoPromotions => ResourceManager.GetString("NoPromotions", resourceCulture);
        public static string DiscountLabel => ResourceManager.GetString("DiscountLabel", resourceCulture);
        public static string ValidWindowLabel => ResourceManager.GetString("ValidWindowLabel", resourceCulture);
        public static string RemoveAction => ResourceManager.GetString("RemoveAction", resourceCulture);
        public static string RedeemSuccess => ResourceManager.GetString("RedeemSuccess", resourceCulture);
    }
}
