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
  using FormDataHandler = Sitecore.Form.Core.FormDataHandler;

  [Designer("System.Windows.Forms.Design.ParentControlDesigner, System.Design", typeof(IDesigner))]
  public class SimpleForm : UserControl
  {
    private readonly IActionExecutor actionExecutor;

    private readonly IAnalyticsTracker analyticsTracker;

    protected HiddenField AntiCsrf;

    private Item contextItem;

    private List<ControlResult> controlResults;

    protected HiddenField EventCounter;

    protected FormItem formItem;

    public static string FormRedirectingFormIdKey = "scwfmformid";

    public static string FormRedirectingHandlerKey = "scwfmformkey";

    public static string FormRedirectingPlaceholderKey = "scwfmformplacehodler";

    public static string FormRedirectingPreviousPageItemKey = "scwfmpageitem";

    public static string FormRedirectingPreviousPageKey = "scwfmprevpage";

    private readonly IItemRepository itemRepository;

    private readonly ILogger logger;

    public static readonly string PrefixAntiCsrfId = "_anticsrf";

    public static readonly string prefixErrorID = "_submitSummary";

    public static readonly string prefixEventCountID = "_eventcount";

    public static readonly string prefixSuccessMessageID = "_successmessage";

    public static readonly string prefixSummaryID = "_summary";

    private ProtectionSchema robotDetection;

    public event EventHandler<EventArgs> FailedSubmit;

    public event EventHandler<EventArgs> SucceedSubmit;

    public event EventHandler<EventArgs> SucceedValidation;

    protected virtual string BaseID
    {
      get
      {
        Control control = this.FindControl("formreference");
        if (control != null)
        {
          return ((HiddenField)control).Value;
        }
        Control control2 = this.FindRoot();
        if (control2 != null && control2.ID == null)
        {
          return string.Empty;
        }
        return string.Empty;
      }
    }

    [Browsable(false)]
    public string CssClass
    {
      get;
      set;
    }

    [Browsable(false)]
    public bool FastPreview
    {
      get;
      set;
    }

    protected FormItem Form
    {
      get
      {
        if (this.formItem == null)
        {
          Item item = this.itemRepository.GetItem(this.FormID);
          if (item != null)
          {
            this.formItem = new FormItem(item);
          }
        }
        return this.formItem;
      }
    }

    public virtual ID FormID
    {
      get
      {
        if (this.Controls.Count > 0)
        {
          string[] array = this.BaseID.Split(new char[]
          {
            '_'
          });
          if (array.Length > 1)
          {
            return ShortID.Parse(array[1]).ToID();
          }
        }
        return Sitecore.Data.ID.Null;
      }
    }

    public bool IsAnalyticsEnabled
    {
      get
      {
        return this.Form != null && this.Form.IsAnalyticsEnabled;
      }
    }

    public bool IsDropoutTrackingEnabled
    {
      get
      {
        return this.Form != null && this.Form.IsDropoutTrackingEnabled;
      }
    }

    protected internal bool IsTresholdRedirect
    {
      get;
      set;
    }

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
        return (DateTime)(this.ViewState["rendered"] ?? DateTime.UtcNow);
      }
      set
      {
        this.ViewState["rendered"] = value;
      }
    }

    protected ProtectionSchema RobotDetection
    {
      get
      {
        if (this.robotDetection == null)
        {
          IAttackProtection attackProtection = (IAttackProtection)WebUtil.FindFirstOrDefault(this, (Control c) => c is IAttackProtection);
          this.robotDetection = ((attackProtection == null) ? ProtectionSchema.NoProtection : attackProtection.RobotDetection);
        }
        return this.robotDetection;
      }
    }

    public SimpleForm() : this(DependenciesManager.ActionExecutor, DependenciesManager.Logger, DependenciesManager.AnalyticsTracker, DependenciesManager.Resolve<IItemRepository>())
    {
    }

    [Obsolete("Use more plenty constructor")]
    public SimpleForm(IActionExecutor actionExecutor, ILogger logger, IAnalyticsTracker analyticsTracker) : this(actionExecutor, logger, analyticsTracker, DependenciesManager.Resolve<IItemRepository>())
    {
    }

    public SimpleForm(IActionExecutor actionExecutor, ILogger logger, IAnalyticsTracker analyticsTracker, IItemRepository itemRepository)
    {
      this.EventCounter = new HiddenField();
      Assert.IsNotNull(actionExecutor, "actionExecutor");
      Assert.IsNotNull(logger, "logger");
      Assert.IsNotNull(analyticsTracker, "analyticsTracker");
      Assert.IsNotNull(itemRepository, "itemRepository");
      this.actionExecutor = actionExecutor;
      this.logger = logger;
      this.analyticsTracker = analyticsTracker;
      this.itemRepository = itemRepository;
    }

    protected virtual void CollectActions(Control source, List<IActionDefinition> list)
    {
      Assert.ArgumentNotNull(source, "source");
      Assert.ArgumentNotNull(list, "list");
      foreach (Control control in source.Controls)
      {
        if (control is ActionControl)
        {
          ActionControl actionControl = control as ActionControl;
          string text = actionControl.ID;
          if (!string.IsNullOrEmpty(text))
          {
            int num = text.IndexOf('_');
            if (num > -1 && num + 1 < text.Length)
            {
              text = text.Substring(num + 1);
            }
          }
          ActionDefinition item = new ActionDefinition(actionControl.ActionID, actionControl.Value)
          {
            UniqueKey = text
          };
          list.Add(item);
        }
        this.CollectActions(control, list);
      }
    }

    private Control FindRoot()
    {
      foreach (Control control in this.Controls)
      {
        if (control is HtmlGenericControl)
        {
          return control;
        }
      }
      return null;
    }

    public List<ControlResult> GetChildState()
    {
      if (this.controlResults == null)
      {
        this.controlResults = new List<ControlResult>();
        SimpleForm.GetChildState(this, this.controlResults);
      }
      return this.controlResults;
    }

    private static void GetChildState(Control parent, List<ControlResult> state)
    {
      foreach (Control control in parent.Controls)
      {
        if (control is IResult)
        {
          IResult result = (IResult)control;
          try
          {
            ControlResult result2 = result.Result;
            result2.FieldID = result.FieldID;
            state.Add(result2);
          }
          catch (ArgumentException)
          {
            throw new ArgumentException(string.Format(DependenciesManager.ResourceManager.Localize("ERROR_IDENTICAL_NAME"), result.FieldID));
          }
        }
        SimpleForm.GetChildState(control, state);
      }
    }

    public bool HasVisibleFields(ID formId)
    {
      Item item = StaticSettings.ContextDatabase.GetItem(formId);
      if (item == null)
      {
        return true;
      }
      int num = 0;
      string query = string.Format(".//*[@@templateid = '{0}']", Sitecore.Form.Core.Configuration.IDs.FieldTemplateID);
      Item[] array = item.Axes.SelectItems(query);
      if (array != null)
      {
        Item[] array2 = array;
        for (int i = 0; i < array2.Length; i++)
        {
          Item item2 = array2[i];
          if (!string.IsNullOrEmpty(item2.Fields["Title"].Value))
          {
            num++;
          }
        }
      }
      return num > 0;
    }

    protected virtual void OnAddInitOnClient()
    {
      StringBuilder stringBuilder = new StringBuilder();
      stringBuilder.Append("$scw(document).ready(function() {");
      stringBuilder.AppendFormat("$scw('#{0}').webform(", this.ID);
      stringBuilder.Append("{");
      stringBuilder.AppendFormat("formId:\"{0}\"", this.FormID);
      if (this.Form != null && this.Form.IsDropoutTrackingEnabled && !this.FastPreview && this.PageItem != null)
      {
        stringBuilder.AppendFormat(", pageId:\"{0}\", pageIndex:\"{1}\", eventCountId:\"{2}\", tracking : true", this.PageItem.ID, (this.logger.IsNull(this.analyticsTracker.Current, "Tracker.Current") || this.logger.IsNull(Tracker.Current.CurrentPage, "Tracker.Current.CurrentPage")) ? string.Empty : this.analyticsTracker.CurrentTrackerCurrentPageVisitPageIndex.ToString(CultureInfo.InvariantCulture), this.EventCounter.ClientID);
      }
      stringBuilder.Append("})});");
      this.Page.ClientScript.RegisterClientScriptBlock(base.GetType(), "sc-client-webform" + this.ClientID, stringBuilder.ToString(), true);
    }

    protected virtual void OnClick(object sender, EventArgs e)
    {
      Assert.ArgumentNotNull(sender, "sender");
      this.UpdateSubmitAnalytics();
      this.UpdateSubmitCounter();
      bool flag = false;
      System.Web.UI.ValidatorCollection validators = (this.Page ?? new Page()).GetValidators(((Control)sender).ID);
      if (validators.FirstOrDefault((IValidator v) => !v.IsValid && v is IAttackProtection) != null)
      {
        validators.ForEach(delegate (IValidator v)
        {
          if (!v.IsValid && !(v is IAttackProtection))
          {
            v.IsValid = true;
          }
        });
      }
      if (this.Page != null && this.Page.IsValid)
      {
        this.RequiredMarkerProccess(this, true);
        List<IActionDefinition> list = new List<IActionDefinition>();
        this.CollectActions(this, list);
        try
        {
          FormDataHandler.ProcessData(this.FormID, this.GetChildState().ToArray(), list.ToArray(), this.actionExecutor);
          this.OnSuccessSubmit();
          this.OnSucceedValidation(new EventArgs());
          this.OnSucceedSubmit(new EventArgs());
          goto IL_232;
        }
        catch (ThreadAbortException)
        {
          flag = true;
          goto IL_232;
        }
        catch (ValidatorException ex)
        {
          string[] messages = new string[]
          {
            ex.Message
          };
          this.OnRefreshError(messages);
          goto IL_232;
        }
        catch (FormSubmitException ex2)
        {
          flag = true;
          this.OnRefreshError((from f in ex2.Failures
                               select f.ErrorMessage).ToArray<string>());
          goto IL_232;
        }
        catch (Exception ex3)
        {
          try
          {
            ExecuteResult.Failure[] array = new ExecuteResult.Failure[1];
            ExecuteResult.Failure failure = new ExecuteResult.Failure
            {
              ErrorMessage = ex3.ToString(),
              StackTrace = ex3.StackTrace
            };
            array[0] = failure;
            SubmittedFormFailuresArgs submittedFormFailuresArgs = new SubmittedFormFailuresArgs(this.FormID, array)
            {
              Database = StaticSettings.ContextDatabase.Name
            };
            CorePipeline.Run("errorSubmit", submittedFormFailuresArgs);
            this.OnRefreshError((from f in submittedFormFailuresArgs.Failures
                                 select f.ErrorMessage).ToArray<string>());
          }
          catch (Exception ex4)
          {
            Log.Error(ex4.Message, ex4, this);
          }
          flag = true;
          goto IL_232;
        }
      }
      this.SetFocusOnError();
      this.TrackValdationEvents(sender, e);
      this.RequiredMarkerProccess(this, false);
      IL_232:
      this.EventCounter.Value = (this.analyticsTracker.EventCounter + 1).ToString(CultureInfo.InvariantCulture);
      if (flag)
      {
        this.OnSucceedValidation(new EventArgs());
      }
      this.OnFailedSubmit(new EventArgs());
    }

    private void OnFailedSubmit(EventArgs e)
    {
      EventHandler<EventArgs> failedSubmit = this.FailedSubmit;
      if (failedSubmit != null)
      {
        failedSubmit(this, e);
      }
    }

    protected override void OnInit(EventArgs e)
    {
      base.OnInit(e);
      if (this.Page == null)
      {
        this.Page = WebUtil.GetPage();
      }
      this.Page.EnableViewState = true;
      this.EventCounter.ID = this.ID + SimpleForm.prefixEventCountID;
      if (this.FindControl(this.EventCounter.ClientID) == null)
      {
        this.Controls.Add(this.EventCounter);
      }
    }

    protected override void OnLoad(EventArgs e)
    {
      base.OnLoad(e);
      this.OnRefreshError(string.Empty);
      if (!this.Page.IsPostBack && !this.Page.IsCallback && !this.IsTresholdRedirect)
      {
        this.RenderedTime = DateTime.UtcNow;
      }
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
      base.OnPreRender(e);
      if (!this.FastPreview && this.Page != null)
      {
        string script = string.Format("$scw('#" + this.ID + "').webform('updateSubmitData', '{0}');", this.ID);
        this.Page.ClientScript.RegisterOnSubmitStatement(base.GetType(), "sc-webform-disable-submit-button" + this.ClientID, script);
      }
    }

    protected virtual void OnRefreshError(string[] messages)
    {
      Assert.ArgumentNotNull(messages, "messages");
      Control control = this.FindControl(this.BaseID + SimpleForm.prefixErrorID);
      if (control != null && control is SubmitSummary)
      {
        SubmitSummary submitSummary = (SubmitSummary)control;
        submitSummary.Messages = messages;
        if (submitSummary.Messages.Length > 0)
        {
          this.SetFocus(control.ClientID, null);
        }
      }
    }

    private void OnRefreshError(string message)
    {
      this.OnRefreshError(new string[]
      {
        message
      });
    }

    private void OnSucceedSubmit(EventArgs e)
    {
      EventHandler<EventArgs> succeedSubmit = this.SucceedSubmit;
      if (succeedSubmit != null)
      {
        succeedSubmit(this, e);
      }
    }

    private void OnSucceedValidation(EventArgs args)
    {
      EventHandler<EventArgs> succeedValidation = this.SucceedValidation;
      if (succeedValidation != null)
      {
        succeedValidation(this, args);
      }
    }

    protected virtual void OnSuccessSubmit()
    {
      HiddenField hiddenField = this.FindControl(this.BaseID + SimpleForm.prefixSuccessMessageID) as HiddenField;
      this.Controls.Clear();
      if (hiddenField != null)
      {
        Literal literal = new Literal();
        SubmitSuccessArgs submitSuccessArgs = new SubmitSuccessArgs(HttpUtility.HtmlEncode(hiddenField.Value));
        CorePipeline.Run("successAction", submitSuccessArgs);
        literal.Text = submitSuccessArgs.Result;
        this.Controls.Add(literal);
        this.SetFocus(this.ID, null);
      }
    }

    protected virtual void OnTrackValidationEvent(object sender, EventArgs e)
    {
      Assert.ArgumentNotNull(sender, "sender");
      if (this.Page != null)
      {
        foreach (BaseValidator baseValidator in this.Page.GetValidators(((Control)sender).ID))
        {
          if (!baseValidator.IsValid && baseValidator.CssClass.Contains("trackevent"))
          {
            string type = HttpContext.Current.Server.UrlDecode(Regex.Replace(baseValidator.CssClass, ".*trackevent[.]|\\s.*", string.Empty));
            string fieldID = HttpContext.Current.Server.UrlDecode(Regex.Replace(baseValidator.CssClass, ".*fieldid[.]|\\s.*", string.Empty));
            ServerEvent serverEvent = new ServerEvent
            {
              FieldID = fieldID,
              FormID = this.FormID.ToString(),
              Type = type,
              Value = baseValidator.ErrorMessage
            };
            this.analyticsTracker.TriggerEvent(serverEvent);
          }
        }
      }
    }

    public override void RenderControl(HtmlTextWriter writer)
    {
      Assert.ArgumentNotNull(writer, "writer");
      if (this.itemRepository.GetItem(this.FormID) != null)
      {
        writer.Write("<div class='{0}' id=\"{1}\"", this.CssClass ?? "scfForm", this.ID);
        base.Attributes.Render(writer);
        writer.Write(">");
        base.RenderControl(writer);
        writer.Write("</div>");
      }
    }

    protected virtual void RequiredMarkerProccess(Control parent, bool visible)
    {
      Assert.ArgumentNotNull(parent, "parent");
      if (parent is MarkerLabel)
      {
        (parent as MarkerLabel).Visible = visible;
      }
      foreach (Control parent2 in parent.Controls)
      {
        this.RequiredMarkerProccess(parent2, visible);
      }
    }

    public void SetChildState(List<ControlResult> state)
    {
      this.SetChildState(this, state);
    }

    protected void SetChildState(Control parent, List<ControlResult> results)
    {
      foreach (Control control in parent.Controls)
      {
        if (control is IResult)
        {
          IResult result = (IResult)control;
          ControlResult controlResult = results.FirstOrDefault((ControlResult x) => x.FieldID == result.FieldID);
          if (controlResult != null)
          {
            result.Result = controlResult;
          }
        }
        this.SetChildState(control, results);
      }
    }

    protected void SetFocus(string id, string focusID)
    {
      StringBuilder stringBuilder = new StringBuilder();
      stringBuilder.Append("$scw(document).ready(function() { $scw('#");
      stringBuilder.Append(this.ID);
      stringBuilder.AppendFormat("').webform('scrollTo', '{0}', '{1}')", id ?? string.Empty, focusID ?? string.Empty);
      stringBuilder.Append("});");
      this.Page.ClientScript.RegisterClientScriptBlock(base.GetType(), "sc-webform-setfocus" + this.ClientID, stringBuilder.ToString(), true);
    }

    private void SetFocusOnError()
    {
      if (this.Page != null)
      {
        BaseValidator baseValidator = (BaseValidator)this.Page.Validators.FirstOrDefault((IValidator v) => v is BaseValidator && ((BaseValidator)v).IsFailedAndRequireFocus());
        if (baseValidator != null)
        {
          if (!string.IsNullOrEmpty(baseValidator.Text))
          {
            Control controlToValidate = baseValidator.GetControlToValidate();
            if (controlToValidate != null)
            {
              this.SetFocus(baseValidator.ClientID, controlToValidate.ClientID);
              return;
            }
          }
          else
          {
            Control control = this.FindControl(this.BaseID + SimpleForm.prefixErrorID);
            if (control != null)
            {
              this.SetFocus(control.ClientID, null);
            }
          }
        }
      }
    }

    private void TrackValdationEvents(object sender, EventArgs e)
    {
      if (this.IsDropoutTrackingEnabled)
      {
        this.OnTrackValidationEvent(sender, e);
      }
    }

    private void UpdateSubmitAnalytics()
    {
      if (this.IsAnalyticsEnabled && !this.FastPreview)
      {
        this.analyticsTracker.BasePageTime = this.RenderedTime;
        this.analyticsTracker.TriggerEvent(Sitecore.WFFM.Abstractions.Analytics.IDs.FormSubmitEventId, "Form Submit", this.FormID, string.Empty, this.FormID.ToString());
      }
    }

    private void UpdateSubmitCounter()
    {
      if (this.RobotDetection.Session.Enabled)
      {
        SubmitCounter.Session.AddSubmit(this.FormID, this.RobotDetection.Session.MinutesInterval);
      }
      if (this.RobotDetection.Server.Enabled)
      {
        SubmitCounter.Server.AddSubmit(this.FormID, this.RobotDetection.Server.MinutesInterval);
      }
    }
  }
}