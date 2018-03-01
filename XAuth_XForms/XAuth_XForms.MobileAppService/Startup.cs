using Microsoft.Owin;
using Owin;

[assembly: OwinStartup(typeof(XAuth_XForms.MobileAppService.Startup))]

namespace XAuth_XForms.MobileAppService
{
    public partial class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            ConfigureMobileApp(app);
        }
    }
}