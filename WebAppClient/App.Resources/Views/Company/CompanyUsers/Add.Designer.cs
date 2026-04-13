namespace App.Resources.Views.Company.CompanyUsers {
    using System;

    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "4.0.0.0")]
    [System.Diagnostics.DebuggerNonUserCodeAttribute()]
    [System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    public class Add {
        private static System.Resources.ResourceManager resourceMan;
        private static System.Globalization.CultureInfo resourceCulture;

        internal Add() {
        }

        public static System.Resources.ResourceManager ResourceManager {
            get {
                if (object.Equals(null, resourceMan)) {
                    var temp = new System.Resources.ResourceManager("WebAppClient.App.Resources.Views.Company.CompanyUsers.Add", typeof(Add).Assembly);
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
        public static string EmailLabel => ResourceManager.GetString("EmailLabel", resourceCulture);
        public static string RoleLabel => ResourceManager.GetString("RoleLabel", resourceCulture);
        public static string NewUserFieldsHint => ResourceManager.GetString("NewUserFieldsHint", resourceCulture);
        public static string FirstNameLabel => ResourceManager.GetString("FirstNameLabel", resourceCulture);
        public static string LastNameLabel => ResourceManager.GetString("LastNameLabel", resourceCulture);
        public static string PhoneLabel => ResourceManager.GetString("PhoneLabel", resourceCulture);
        public static string PasswordLabel => ResourceManager.GetString("PasswordLabel", resourceCulture);
        public static string ConfirmPasswordLabel => ResourceManager.GetString("ConfirmPasswordLabel", resourceCulture);
        public static string SubmitAction => ResourceManager.GetString("SubmitAction", resourceCulture);
    }
}
