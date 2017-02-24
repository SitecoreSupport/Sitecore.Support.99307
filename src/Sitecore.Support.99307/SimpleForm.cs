using Sitecore.Analytics;
using Sitecore.Data;
using Sitecore.Data.Items;
using Sitecore.Diagnostics;
using Sitecore.Form.Core;
using Sitecore.Form.Core.Ascx.Controls;
//using Sitecore.Form.Core.Ascx.Controls;
using Sitecore.Form.Core.Client.Submit;
using Sitecore.Form.Core.Configuration;
using Sitecore.Form.Core.Controls;
using Sitecore.Form.Core.Data;
using Sitecore.Form.Core.Pipelines.FormSubmit;
using Sitecore.Form.Core.Utility;
using Sitecore.Form.Web.UI.Controls;
using Sitecore.Forms.Core.Data;
using Sitecore.Forms.Core.Handlers;
using Sitecore.Pipelines;
using Sitecore.Web.UI.Sheer;
using Sitecore.WFFM.Abstractions.Actions;
using Sitecore.WFFM.Abstractions.Analytics;
using Sitecore.WFFM.Abstractions.Dependencies;
using Sitecore.WFFM.Abstractions.Shared;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web;
using System.Web.UI;
using System.Web.UI.HtmlControls;
using System.Web.UI.WebControls;

namespace Sitecore.Support.Form.Core.Ascx.Controls
{
    [Designer("System.Windows.Forms.Design.ParentControlDesigner, System.Design", typeof(IDesigner))]
    public class SimpleForm : UserControl
    {
        //sc8.2.2 updates
        private readonly FormDataHandler formDataHandler;
        


        public static string FormRedirectingFormIdKey = "scwfmformid";
        public static string FormRedirectingHandlerKey = "scwfmformkey";
        public static string FormRedirectingPlaceholderKey = "scwfmformplacehodler";
        public static string FormRedirectingPreviousPageItemKey = "scwfmpageitem";
        public static string FormRedirectingPreviousPageKey = "scwfmprevpage";
        public static readonly string PrefixAntiCsrfId = "_anticsrf";
        public static readonly string prefixErrorID = "_submitSummary";
        public static readonly string prefixEventCountID = "_eventcount";
        public static readonly string prefixSuccessMessageID = "_successmessage";
        public static readonly string prefixSummaryID = "_summary";
        //private readonly IActionExecutor actionExecutor;


        private readonly IAnalyticsTracker analyticsTracker;
        protected HiddenField AntiCsrf;
        private Item contextItem;
        private List<ControlResult> controlResults;
        protected HiddenField EventCounter;
        protected FormItem formItem;
        private readonly IItemRepository itemRepository;
        private readonly ILogger logger;
        private ProtectionSchema robotDetection;

        protected virtual string BaseID
        {
            get
            {
                Control control = this.FindControl("formreference");
                if (control != null)
                    return ((HiddenField)control).Value;
                Control root = this.FindRoot();
                if (root != null && root.ID == null)
                    return string.Empty;
                return string.Empty;
            }
        }

        [Browsable(false)]
        public string CssClass { get; set; }

        [Browsable(false)]
        public bool FastPreview { get; set; }

        protected FormItem Form
        {
            get
            {
                if (this.formItem == null)
                {
                    Item obj = this.itemRepository.GetItem(this.FormID);
                    if (obj != null)
                        this.formItem = new FormItem(obj);
                }
                return this.formItem;
            }
        }

        public virtual Sitecore.Data.ID FormID
        {
            get
            {
                if (this.Controls.Count > 0)
                {
                    string[] strArray = this.BaseID.Split('_');
                    if (strArray.Length > 1)
                        return ShortID.Parse(strArray[1]).ToID();
                }
                return Sitecore.Data.ID.Null;
            }
        }

        public bool IsAnalyticsEnabled
        {
            get
            {
                if (this.Form != null)
                    return this.Form.IsAnalyticsEnabled;
                return false;
            }
        }

        public bool IsDropoutTrackingEnabled
        {
            get
            {
                if (this.Form != null)
                    return this.Form.IsDropoutTrackingEnabled;
                return false;
            }
        }

        protected internal bool IsTresholdRedirect { get; set; }

        public Item PageItem
        {
            get
            {
                return Sitecore.Context.Item ?? this.contextItem;
            }
            internal set
            {
                this.contextItem = value;
            }
        }

        protected internal DateTime RenderedTime
        {
            get
            {
                return (DateTime)(this.ViewState["rendered"] ?? (object)DateTime.UtcNow);
            }
            set
            {
                this.ViewState["rendered"] = (object)value;
            }
        }

        protected ProtectionSchema RobotDetection
        {
            get
            {
                if (this.robotDetection == null)
                {
                    IAttackProtection attackProtection = (IAttackProtection)WebUtil.FindFirstOrDefault((Control)this, (Func<Control, bool>)(c => c is IAttackProtection));
                    this.robotDetection = attackProtection == null ? ProtectionSchema.NoProtection : attackProtection.RobotDetection;
                }
                return this.robotDetection;
            }
        }

        public event EventHandler<EventArgs> FailedSubmit;

        public event EventHandler<EventArgs> SucceedSubmit;

        public event EventHandler<EventArgs> SucceedValidation;

        public SimpleForm()
          : this(DependenciesManager.Resolve<FormDataHandler>(), DependenciesManager.Logger, DependenciesManager.AnalyticsTracker, DependenciesManager.Resolve<IItemRepository>())
        {
        }

        //[Obsolete("Use more plenty constructor")]
        //public SimpleForm(IActionExecutor actionExecutor, ILogger logger, IAnalyticsTracker analyticsTracker)
        //  : this(actionExecutor, logger, analyticsTracker, DependenciesManager.Resolve<IItemRepository>())
        //{
        //}

        public SimpleForm(FormDataHandler formDataHandler, ILogger logger, IAnalyticsTracker analyticsTracker, IItemRepository itemRepository)
        {
            this.EventCounter = new HiddenField();
            //Assert.IsNotNull((object)actionExecutor, "actionExecutor");
            Assert.ArgumentNotNull((object)formDataHandler, "formDataHandler");

            Assert.IsNotNull((object)logger, "logger");
            Assert.IsNotNull((object)analyticsTracker, "analyticsTracker");
            Assert.IsNotNull((object)itemRepository, "itemRepository");
            //this.actionExecutor = actionExecutor;
            this.formDataHandler = formDataHandler;
            this.logger = logger;
            this.analyticsTracker = analyticsTracker;
            this.itemRepository = itemRepository;
        }

        protected virtual void CollectActions(Control source, List<IActionDefinition> list)
        {
            Assert.ArgumentNotNull((object)source, "source");
            Assert.ArgumentNotNull((object)list, "list");
            foreach (Control control in source.Controls)
            {
                if (control is ActionControl)
                {
                    ActionControl actionControl = control as ActionControl;
                    string str = actionControl.ID;
                    if (!string.IsNullOrEmpty(str))
                    {
                        int num = str.IndexOf('_');
                        if (num > -1 && num + 1 < str.Length)
                            str = str.Substring(num + 1);
                    }
                    //ActionDefinition actionDefinition = new ActionDefinition(actionControl.ActionID, actionControl.Value)
                    //{
                    //    UniqueKey = str
                    //};
                    //list.Add((IActionDefinition)actionDefinition);
                    list.Add((IActionDefinition)new ActionDefinition(actionControl.ActionID, actionControl.Value)
                    {
                        UniqueKey = str
                    });
                }
                this.CollectActions(control, list);
            }
        }

        private Control FindRoot()
        {
            foreach (Control control in this.Controls)
            {
                if (control is HtmlGenericControl)
                    return control;
            }
            return (Control)null;
        }

        public List<ControlResult> GetChildState()
        {
            if (this.controlResults == null)
            {
                this.controlResults = new List<ControlResult>();
                SimpleForm.GetChildState((Control)this, this.controlResults);
            }
            return this.controlResults;
        }

        private static void GetChildState(Control parent, List<ControlResult> state)
        {
            foreach (Control control in parent.Controls)
            {
                if (control is IResult)
                {
                    IResult result1 = (IResult)control;
                    try
                    {
                        ControlResult result2 = result1.Result;
                        result2.FieldID = result1.FieldID;
                        state.Add(result2);
                    }
                    catch (ArgumentException ex)
                    {
                        throw new ArgumentException(string.Format(DependenciesManager.ResourceManager.Localize("ERROR_IDENTICAL_NAME"), (object)result1.FieldID));
                    }
                }
                SimpleForm.GetChildState(control, state);
            }
        }

        public bool HasVisibleFields(ID formId)
        {
            Item obj = StaticSettings.ContextDatabase.GetItem(formId);
            if (obj == null)
                return true;
            int num = 0;
            string query = string.Format(".//*[@@templateid = '{0}']", (object)Sitecore.Form.Core.Configuration.IDs.FieldTemplateID);
            Item[] objArray = obj.Axes.SelectItems(query);
            if (objArray != null)
            {
                foreach (BaseItem baseItem in objArray)
                {
                    if (!string.IsNullOrEmpty(baseItem.Fields["Title"].Value))
                        ++num;
                }
            }
            return num > 0;
        }

        protected virtual void OnAddInitOnClient()
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append("$scw(document).ready(function() {");
            stringBuilder.AppendFormat("$scw('#{0}').webform(", (object)this.ID);
            stringBuilder.Append("{");
            stringBuilder.AppendFormat("formId:\"{0}\"", (object)this.FormID);
            if (this.Form != null && this.Form.IsDropoutTrackingEnabled && (!this.FastPreview && this.PageItem != null))
                stringBuilder.AppendFormat(", pageId:\"{0}\", pageIndex:\"{1}\", eventCountId:\"{2}\", tracking : true", (object)this.PageItem.ID, this.logger.IsNull((object)this.analyticsTracker.Current, "Tracker.Current") || this.logger.IsNull((object)Tracker.Current.CurrentPage, "Tracker.Current.CurrentPage") ? (object)string.Empty : (object)this.analyticsTracker.CurrentTrackerCurrentPageVisitPageIndex.ToString((IFormatProvider)CultureInfo.InvariantCulture), (object)this.EventCounter.ClientID);
            stringBuilder.Append("})});");
            this.Page.ClientScript.RegisterClientScriptBlock(this.GetType(), "sc-client-webform" + this.ClientID, stringBuilder.ToString(), true);
        }

        protected virtual void OnClick(object sender, EventArgs e)
        {
            Assert.ArgumentNotNull(sender, "sender");
            this.UpdateSubmitAnalytics();
            this.UpdateSubmitCounter();
            bool flag = false;
            System.Web.UI.ValidatorCollection validators = (this.Page ?? new Page()).GetValidators(((Control)sender).ID);
            if (validators.FirstOrDefault((Func<IValidator, bool>)(v =>
            {
                if (!v.IsValid)
                    return v is IAttackProtection;
                return false;
            })) != null)
                validators.ForEach((Action<IValidator>)(v =>
                {
                    if (v.IsValid || v is IAttackProtection)
                        return;
                    v.IsValid = true;
                }));

            if (this.Page != null && this.Page.IsValid)
            {
                this.RequiredMarkerProccess((Control)this, true);
                List<IActionDefinition> list = new List<IActionDefinition>();
                this.CollectActions((Control)this, list);
                try
                {
                    //FormDataHandler.ProcessData(this.FormID, this.GetChildState().ToArray(), list.ToArray(), this.actionExecutor);
                    this.formDataHandler.ProcessForm(this.FormID, this.GetChildState().ToArray(), list.ToArray());
                    this.OnSuccessSubmit();
                    this.OnSucceedValidation(new EventArgs());
                    this.OnSucceedSubmit(new EventArgs());
                }
                catch (ThreadAbortException ex)
                {
                    flag = true;
                }
                catch (ValidatorException ex)
                {
                    this.OnRefreshError(new string[1] { ex.Message });
                }
                catch (FormSubmitException ex)
                {
                    flag = true;
                    this.OnRefreshError(ex.Failures.Select<ExecuteResult.Failure, string>((Func<ExecuteResult.Failure, string>)(f => f.ErrorMessage)).ToArray<string>());
                }
                catch (Exception ex1)
                {
                    try
                    {
                        ExecuteResult.Failure[] failureArray = new ExecuteResult.Failure[1];
                        ExecuteResult.Failure failure = new ExecuteResult.Failure()
                        {
                            ErrorMessage = ex1.ToString(),
                            StackTrace = ex1.StackTrace
                        };
                        failureArray[0] = failure;
                        SubmittedFormFailuresArgs formFailuresArgs1 = new SubmittedFormFailuresArgs(this.FormID, (IEnumerable<ExecuteResult.Failure>)failureArray);
                        formFailuresArgs1.Database = StaticSettings.ContextDatabase.Name;
                        SubmittedFormFailuresArgs formFailuresArgs2 = formFailuresArgs1;
                        CorePipeline.Run("errorSubmit", (PipelineArgs)formFailuresArgs2);
                        this.OnRefreshError(((IEnumerable<ExecuteResult.Failure>)formFailuresArgs2.Failures).Select<ExecuteResult.Failure, string>((Func<ExecuteResult.Failure, string>)(f => f.ErrorMessage)).ToArray<string>());
                    }
                    catch (Exception ex2)
                    {
                        Log.Error(ex2.Message, ex2, (object)this);
                    }
                    flag = true;
                }
            }
            else
            {
                this.SetFocusOnError();
                this.TrackValdationEvents(sender, e);
                this.RequiredMarkerProccess((Control)this, false);
            }
            this.EventCounter.Value = (this.analyticsTracker.EventCounter + 1).ToString((IFormatProvider)CultureInfo.InvariantCulture);
            if (flag)
                this.OnSucceedValidation(new EventArgs());
            this.OnFailedSubmit(new EventArgs());
        }

        private void OnFailedSubmit(EventArgs e)
        {
            EventHandler<EventArgs> eventHandler = this.FailedSubmit;
            if (eventHandler == null)
                return;
            eventHandler((object)this, e);
        }

        protected override void OnInit(EventArgs e)
        {
            base.OnInit(e);
            if (this.Page == null)
                this.Page = WebUtil.GetPage();
            this.Page.EnableViewState = true;
            this.EventCounter.ID = this.ID + SimpleForm.prefixEventCountID;
            if (this.FindControl(this.EventCounter.ClientID) != null)
                return;
            this.Controls.Add((Control)this.EventCounter);
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            this.OnRefreshError(string.Empty);
            if (!this.Page.IsPostBack && !this.Page.IsCallback && !this.IsTresholdRedirect)
                this.RenderedTime = DateTime.UtcNow;
            this.Page.ClientScript.RegisterClientScriptInclude("jquery", "/sitecore modules/web/web forms for marketers/scripts/jquery.js");
            this.Page.ClientScript.RegisterClientScriptInclude("jquery-ui.min", "/sitecore modules/web/web forms for marketers/scripts/jquery-ui.min.js");
            this.Page.ClientScript.RegisterClientScriptInclude("jquery-ui-i18n", "/sitecore modules/web/web forms for marketers/scripts/jquery-ui-i18n.js");
            this.Page.ClientScript.RegisterClientScriptInclude("json2.min", "/sitecore modules/web/web forms for marketers/scripts/json2.min.js");
            this.Page.ClientScript.RegisterClientScriptInclude("head.load.min", "/sitecore modules/web/web forms for marketers/scripts/head.load.min.js");
            this.Page.ClientScript.RegisterClientScriptInclude("sc.webform", "/sitecore modules/web/web forms for marketers/scripts/sc.webform.js?v=17072012");
            if (this.IsAnalyticsEnabled && !this.FastPreview)
            {
                this.analyticsTracker.BasePageTime = this.RenderedTime;
                this.EventCounter.Value = (this.analyticsTracker.EventCounter + 1).ToString();
            }
            this.OnAddInitOnClient();
            this.Page.PreRenderComplete += new EventHandler(this.OnPreRenderComplete);
        }

        protected void OnPreRenderComplete(object sender, EventArgs e)
        {
            this.OnPreRender(e);
            if (this.FastPreview || this.Page == null)
                return;
            this.Page.ClientScript.RegisterOnSubmitStatement(this.GetType(), "sc-webform-disable-submit-button" + this.ClientID, string.Format("$scw('#" + this.ID + "').webform('updateSubmitData', '{0}');", (object)this.ID));
        }

        protected virtual void OnRefreshError(string[] messages)
        {
            Assert.ArgumentNotNull((object)messages, "messages");
            Control control = this.FindControl(this.BaseID + SimpleForm.prefixErrorID);
            if (control == null || !(control is SubmitSummary))
                return;
            SubmitSummary submitSummary = (SubmitSummary)control;
            submitSummary.Messages = messages;
            if (submitSummary.Messages.Length <= 0)
                return;
            this.SetFocus(control.ClientID, (string)null);
        }

        private void OnRefreshError(string message)
        {
            this.OnRefreshError(new string[1] { message });
        }

        private void OnSucceedSubmit(EventArgs e)
        {
            EventHandler<EventArgs> eventHandler = this.SucceedSubmit;
            if (eventHandler == null)
                return;
            eventHandler((object)this, e);
        }

        private void OnSucceedValidation(EventArgs args)
        {
            EventHandler<EventArgs> eventHandler = this.SucceedValidation;
            if (eventHandler == null)
                return;
            eventHandler((object)this, args);
        }

        protected virtual void OnSuccessSubmit()
        {
            HiddenField hiddenField = this.FindControl(this.BaseID + SimpleForm.prefixSuccessMessageID) as HiddenField;
            this.Controls.Clear();
            if (hiddenField == null)
                return;
            Literal literal = new Literal();
            SubmitSuccessArgs submitSuccessArgs = new SubmitSuccessArgs(HttpUtility.HtmlEncode(hiddenField.Value));
            CorePipeline.Run("successAction", (PipelineArgs)submitSuccessArgs);
            literal.Text = ((ClientPipelineArgs)submitSuccessArgs).Result;
            this.Controls.Add((Control)literal);
            this.SetFocus(this.ID, (string)null);
        }

        protected virtual void OnTrackValidationEvent(object sender, EventArgs e)
        {
            Assert.ArgumentNotNull(sender, "sender");
            if (this.Page == null)
                return;
            foreach (BaseValidator validator in this.Page.GetValidators(((Control)sender).ID))
            {
                if (!validator.IsValid && validator.CssClass.Contains("trackevent"))
                {
                    string str1 = HttpContext.Current.Server.UrlDecode(Regex.Replace(validator.CssClass, ".*trackevent[.]|\\s.*", string.Empty));
                    string str2 = HttpContext.Current.Server.UrlDecode(Regex.Replace(validator.CssClass, ".*fieldid[.]|\\s.*", string.Empty));
                    analyticsTracker.TriggerEvent(new ServerEvent()
                    {
                        FieldID = str2,
                        FormID = this.FormID.ToString(),
                        Type = str1,
                        Value = validator.ErrorMessage
                    });
                }
            }
        }

        public override void RenderControl(HtmlTextWriter writer)
        {
            Assert.ArgumentNotNull((object)writer, "writer");
            if (this.itemRepository.GetItem(this.FormID) == null)
                return;
            writer.Write("<div class='{0}' id=\"{1}\"", (object)(this.CssClass ?? "scfForm"), (object)this.ID);
            this.Attributes.Render(writer);
            writer.Write(">");
            base.RenderControl(writer);
            writer.Write("</div>");
        }

        protected virtual void RequiredMarkerProccess(Control parent, bool visible)
        {
            Assert.ArgumentNotNull((object)parent, "parent");
            if (parent is MarkerLabel)
                (parent as MarkerLabel).Visible = visible;
            foreach (Control control in parent.Controls)
                this.RequiredMarkerProccess(control, visible);
        }

        public void SetChildState(List<ControlResult> state)
        {
            this.SetChildState((Control)this, state);
        }

        protected void SetChildState(Control parent, List<ControlResult> results)
        {
            foreach (Control control in parent.Controls)
            {
                if (control is IResult)
                {
                    IResult result = (IResult)control;
                    ControlResult controlResult = results.FirstOrDefault<ControlResult>((Func<ControlResult, bool>)(x => x.FieldID == result.FieldID));
                    if (controlResult != null)
                        result.Result = controlResult;
                }
                this.SetChildState(control, results);
            }
        }

        protected void SetFocus(string id, string focusID)
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append("$scw(document).ready(function() { $scw('#");
            stringBuilder.Append(this.ID);
            stringBuilder.AppendFormat("').webform('scrollTo', '{0}', '{1}')", (object)(id ?? string.Empty), (object)(focusID ?? string.Empty));
            stringBuilder.Append("});");
            this.Page.ClientScript.RegisterClientScriptBlock(this.GetType(), "sc-webform-setfocus" + this.ClientID, stringBuilder.ToString(), true);
        }

        private void SetFocusOnError()
        {
            if (this.Page == null)
                return;
            BaseValidator validator = (BaseValidator)this.Page.Validators.FirstOrDefault((Func<IValidator, bool>)(v =>
            {
                if (v is BaseValidator)
                    return ((BaseValidator)v).IsFailedAndRequireFocus();
                return false;
            }));
            if (validator == null)
                return;
            if (!string.IsNullOrEmpty(validator.Text))
            {
                Control controlToValidate = validator.GetControlToValidate();
                if (controlToValidate == null)
                    return;
                this.SetFocus(validator.ClientID, controlToValidate.ClientID);
            }
            else
            {
                Control control = this.FindControl(this.BaseID + SimpleForm.prefixErrorID);
                if (control == null)
                    return;
                this.SetFocus(control.ClientID, (string)null);
            }
        }

        private void TrackValdationEvents(object sender, EventArgs e)
        {
            if (!this.IsDropoutTrackingEnabled)
                return;
            this.OnTrackValidationEvent(sender, e);
        }

        private void UpdateSubmitAnalytics()
        {
            if (!this.IsAnalyticsEnabled || this.FastPreview)
                return;
            this.analyticsTracker.BasePageTime = this.RenderedTime;
            this.analyticsTracker.TriggerEvent(Sitecore.WFFM.Abstractions.Analytics.IDs.FormSubmitEventId, "Form Submit", this.FormID, string.Empty, this.FormID.ToString());
        }

        private void UpdateSubmitCounter()
        {
            if (this.RobotDetection.Session.Enabled)
                SubmitCounter.Session.AddSubmit(this.FormID, this.RobotDetection.Session.MinutesInterval);
            if (!this.RobotDetection.Server.Enabled)
                return;
            SubmitCounter.Server.AddSubmit(this.FormID, this.RobotDetection.Server.MinutesInterval);
        }
    }
}