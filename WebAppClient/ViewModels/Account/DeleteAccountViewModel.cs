using System.ComponentModel.DataAnnotations;

namespace WebApp.ViewModels.Account;

public class DeleteAccountViewModel
{
    [DataType(DataType.Password)]
    public string? Password { get; set; }
}
