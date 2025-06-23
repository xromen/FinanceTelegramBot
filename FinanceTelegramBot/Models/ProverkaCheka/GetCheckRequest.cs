namespace FinanceTelegramBot.Models.ProverkaCheka;

public class GetCheckRequest
{
    public string Qrurl { get; set; }
    public string Qrfile { get; set; }
    public string Qrraw { get; set; }
    public Manual Manual { get; set; }
}
