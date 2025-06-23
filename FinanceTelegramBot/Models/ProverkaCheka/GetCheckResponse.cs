namespace FinanceTelegramBot.Models.ProverkaCheka;

public class GetCheckResponse
{
    /// <summary>
    /// КОД ОТВЕТА:
    ///     0 - чек некорректен,
    ///     1 - данные чека получены(успешный запрос),
    ///     2 - данные чека пока не получены,
    ///     3 - превышено кол-во запросов,
    ///     4 - ожидание перед повторным запросом,
    ///     5 - прочее(данные не получены)
    /// </summary>
    public int Code { get; set; }
    public int First { get; set; }
    public CheckData Data { get; set; }
    public GetCheckRequest Request { get; set; }
}
