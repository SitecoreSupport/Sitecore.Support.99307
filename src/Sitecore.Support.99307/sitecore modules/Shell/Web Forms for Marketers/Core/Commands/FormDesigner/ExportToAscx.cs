using System;
using System.Collections.Specialized;
using Sitecore.Data;
using Sitecore.Diagnostics;
using Sitecore.Form.Core.Configuration;
using Sitecore.Form.Core.Pipeline.ParseAscx;
using Sitecore.Form.Core.Renderings;
using Sitecore.Form.Core.SchemaGenerator;
using Sitecore.Form.Core.Utility;
using Sitecore.Form.Web.UI.Controls;
using Sitecore.Globalization;
using Sitecore.Layouts;
using Sitecore.Pipelines;
using Sitecore.Shell.Framework.Commands;
using Sitecore.Text;
using Sitecore.Web.UI.Sheer;
using Version = Sitecore.Data.Version;
using WebUtil = Sitecore.Web.WebUtil;

namespace Sitecore.Support.Form.Core.FormDesigner
{
    [Serializable]
    public class ExportToAscx : Sitecore.Form.Core.FormDesigner.ExportToAscx
    {

        /// <summary>
        ///     Runs the specified args.
        /// </summary>
        /// <param name="args">The args.</param>
        protected new void Run(ClientPipelineArgs args)
        {
            var uri = new ItemUri(ID.Parse(args.Parameters["id"]), Language.Parse(args.Parameters["la"]),
                Version.Parse(args.Parameters["vs"]), args.Parameters["db"]);

            var item = Database.GetItem(uri);

            if (item != null)
                if (!args.IsPostBack)
                {
                    using (new LanguageSwitcher(item.Language))
                    {
                        var reference = new RenderingReference(item.Database.GetItem(IDs.FormInterpreterID));
                        reference.Settings.Parameters = "FormID=" + item.ID;

                        var control = (FormRender)reference.RenderingItem.GetControl(reference.Settings);
                        control.IsClearDepend = true;
                        control.InitControls();

                        var form = (SitecoreSimpleForm)control.Controls[0];

                        var scripts = ThemesManager.ScriptsTags(SiteUtils.GetFormsRootItemForItem(item), item);

                        var controlDirectiva =
                            "<%@ Control Language=\"C#\" AutoEventWireup=\"true\" CodeBehind=\"SimpleForm.cs\" Inherits=\"Sitecore.Support.Form.Core.Ascx.Controls.SimpleForm\" %>";

                        string schema = SchemaGeneratorManager.GetSchema(form, controlDirectiva, scripts);
                        var parseAscxArgs = new ParseAscxArgs(schema);
                        CorePipeline.Run("parseAscx", parseAscxArgs);
                        WebUtil.SetSessionValue("filecontent", parseAscxArgs.AscxContent);
                    }

                    var str = new UrlString(UIUtil.GetUri("control:Forms.SaveToAscx.Preview"));
                    SheerResponse.ShowModalDialog(str.ToString(), true);

                    args.WaitForPostBack();
                }
        }
    }
}