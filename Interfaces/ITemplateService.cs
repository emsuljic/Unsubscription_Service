namespace UnsubscribeService.Interfaces
{
    public interface ITemplateService
    {
        string GetTemplateById(Guid templateId);
        string GetDefaultTemplate();
    }
}
