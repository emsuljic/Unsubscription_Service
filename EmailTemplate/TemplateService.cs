using UnsubscribeService.Interfaces;

namespace UnsubscribeService.EmailTemplate
{
    public class TemplateService : ITemplateService
    {
        private readonly Dictionary<Guid, string> _templates;

        public TemplateService(){}

        public string GetTemplateById(Guid templateId)
        {
            return _templates.TryGetValue(templateId, out var template) ? template : null;
        }

        public string GetDefaultTemplate()
        {
            return "<html>Default template with token: {0} and email: {1}</html>";
        }
    }
}
