namespace FinanceTelegramBot.Models.ProverkaCheka;

public class JsonData
{
    public int Code { get; set; }
    public string User { get; set; }
    public List<Item> Items { get; set; }
    public int Nds18 { get; set; }
    public string FnsUrl { get; set; }
    public string Region { get; set; }
    public string UserInn { get; set; }
    public DateTime DateTime { get; set; }
    public string KktRegId { get; set; }
    public Metadata Metadata { get; set; }
    public int TotalSum { get; set; }
    public int CreditSum { get; set; }
    public string NumberKkt { get; set; }
    public long FiscalSign { get; set; }
    public int PrepaidSum { get; set; }
    public string RetailPlace { get; set; }
    public int ShiftNumber { get; set; }
    public int CashTotalSum { get; set; }
    public int ProvisionSum { get; set; }
    public int EcashTotalSum { get; set; }
    public string MachineNumber { get; set; }
    public int OperationType { get; set; }
    public int Redefine_mask { get; set; }
    public int RequestNumber { get; set; }
    public string FiscalDriveNumber { get; set; }
    public double MessageFiscalSign { get; set; }
    public string RetailPlaceAddress { get; set; }
    public int AppliedTaxationType { get; set; }
    public int FiscalDocumentNumber { get; set; }
    public int FiscalDocumentFormatVer { get; set; }
}
