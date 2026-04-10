namespace App.Resources.Views.Company.CompanyUsers {
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
                    var temp = new System.Resources.ResourceManager("App.Resources.Views.Company.CompanyUsers.Index", typeof(Index).Assembly);
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
        public static string AddAction => ResourceManager.GetString("AddAction", resourceCulture);
        public static string BackToDashboard => ResourceManager.GetString("BackToDashboard", resourceCulture);
        public static string NoUsers => ResourceManager.GetString("NoUsers", resourceCulture);
        public static string RoleLabel => ResourceManager.GetString("RoleLabel", resourceCulture);
        public static string JoinedAtLabel => ResourceManager.GetString("JoinedAtLabel", resourceCulture);
        public static string ActiveStatus => ResourceManager.GetString("ActiveStatus", resourceCulture);
        public static string InactiveStatus => ResourceManager.GetString("InactiveStatus", resourceCulture);
        public static string EditAction => ResourceManager.GetString("EditAction", resourceCulture);
        public static string RemoveAction => ResourceManager.GetString("RemoveAction", resourceCulture);
    }
}
