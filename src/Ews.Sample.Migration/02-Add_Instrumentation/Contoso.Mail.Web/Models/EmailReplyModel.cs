using System.ComponentModel.DataAnnotations;

namespace Contoso.Mail.Models;

public class EmailReplyModel
{
    [Required]
    public string Id { get; set; } = string.Empty;
    
    [Required]
    public string Subject { get; set; } = string.Empty;
    
    [Required]
    public string To { get; set; } = string.Empty;
    
    [Required]
    [DataType(DataType.MultilineText)]
    public string Body { get; set; } = string.Empty;
}