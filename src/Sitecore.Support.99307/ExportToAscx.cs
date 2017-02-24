using Sitecore.Data;
using Sitecore.Data.Items;
using Sitecore.Form.Core.Configuration;
using Sitecore.Form.Core.Pipeline.ParseAscx;
using Sitecore.Form.Core.Renderings;
using Sitecore.Form.Core.SchemaGenerator;
using Sitecore.Form.Core.Utility;
using Sitecore.Form.Web.UI.Controls;
using Sitecore.Globalization;
using Sitecore.Layouts;
using Sitecore.Pipelines;
using Sitecore.Text;
using Sitecore.Web.UI.Sheer;
using System;
using System.Web.UI;

namespace Sitecore.Support.Form.Core.FormDesigner
{
    [Serializable]
    public class ExportToAscx : Sitecore.Form.Core.FormDesigner.ExportToAscx
    {
        protected new void Run(ClientPipelineArgs args)
        {
            Item contextItem = Database.GetItem(new ItemUri(ID.Parse(args.Parameters["id"]), Language.Parse(args.Parameters["la"]), Sitecore.Data.Version.Parse(args.Parameters["vs"]), args.Parameters["db"]));
            if (contextItem == null || args.IsPostBack)
                return;
            using (new LanguageSwitcher(contextItem.Language))
            {
                RenderingReference renderingReference = new RenderingReference((RenderingItem)contextItem.Database.GetItem(IDs.FormInterpreterID))
                {
                    Settings = {
            Parameters = "FormID=" + (object) contextItem.ID
          }
                };
                FormRender formRender = (FormRender)renderingReference.RenderingItem.GetControl(renderingReference.Settings);
                formRender.IsClearDepend = true;
                formRender.InitControls();
                SitecoreSimpleForm sitecoreSimpleForm = (SitecoreSimpleForm)formRender.Controls[0];
                string[] scripts = ThemesManager.ScriptsTags(SiteUtils.GetFormsRootItemForItem(contextItem), contextItem);
                string controlDirective = "<%@ Control Language=\"C#\" AutoEventWireup=\"true\" CodeBehind=\"SimpleForm.cs\" Inherits=\"Sitecore.Support.Form.Core.Ascx.Controls.SimpleForm\" %>";
                ParseAscxArgs parseAscxArgs = new ParseAscxArgs(SchemaGeneratorManager.GetSchema((Control)sitecoreSimpleForm, controlDirective, scripts));
                CorePipeline.Run("parseAscx", (PipelineArgs)parseAscxArgs);
                Sitecore.Web.WebUtil.SetSessionValue("filecontent", (object)parseAscxArgs.AscxContent);
            }
            SheerResponse.ShowModalDialog(new UrlString(UIUtil.GetUri("control:Forms.SaveToAscx.Preview")).ToString(), true);
            args.WaitForPostBack();
        }
    }
}
