namespace App.Resources.Views.Company.CompanyUsers {
    using System;

    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "4.0.0.0")]
    [System.Diagnostics.DebuggerNonUserCodeAttribute()]
    [System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    public class AddResult {
        private static System.Resources.ResourceManager resourceMan;
        private static System.Globalization.CultureInfo resourceCulture;

        internal AddResult() {
        }

        public static System.Resources.ResourceManager ResourceManager {
            get {
                if (object.Equals(null, resourceMan)) {
                    var temp = new System.Resources.ResourceManager("App.Resources.Views.Company.CompanyUsers.AddResult", typeof(AddResult).Assembly);
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
        public static string BackAction => ResourceManager.GetString("BackAction", resourceCulture);
        public static string RoleLabel => ResourceManager.GetString("RoleLabel", resourceCulture);
        public static string AccessStatusLabel => ResourceManager.GetString("AccessStatusLabel", resourceCulture);
        public static string NextActionLabel => ResourceManager.GetString("NextActionLabel", resourceCulture);
        public static string AlreadyActiveStatus => ResourceManager.GetString("AlreadyActiveStatus", resourceCulture);
        public static string ReactivatedStatus => ResourceManager.GetString("ReactivatedStatus", resourceCulture);
        public static string NewUserStatus => ResourceManager.GetString("NewUserStatus", resourceCulture);
        public static string LinkedStatus => ResourceManager.GetString("LinkedStatus", resourceCulture);
        public static string TemporaryPasswordLabel => ResourceManager.GetString("TemporaryPasswordLabel", resourceCulture);
    }
}
