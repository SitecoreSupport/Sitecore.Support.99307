using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Linq;
using System.Threading;
using System.Web.UI;
using Sitecore.Diagnostics;
using Sitecore.Form.Core.Client.Submit;
using Sitecore.Form.Core.Configuration;
using Sitecore.Form.Core.Data;
using Sitecore.Form.Core.Pipelines.FormSubmit;
using Sitecore.Form.Core.Utility;
using Sitecore.Form.Web.UI.Controls;
using Sitecore.Forms.Core.Handlers;
using Sitecore.Pipelines;
using Sitecore.WFFM.Abstractions.Actions;
using Sitecore.WFFM.Abstractions.Dependencies;
using Sitecore.WFFM.Abstractions.Shared;

namespace Sitecore.Support.Form.Core.Ascx.Controls
{
    /// <summary>
    /// The simple form.
    /// </summary>
    [Designer("System.Windows.Forms.Design.ParentControlDesigner, System.Design", typeof(IDesigner))]
    public class SimpleForm : Sitecore.Form.Core.Ascx.Controls.SimpleForm
    {
        private readonly FormDataHandler formDataHandler;

        /// <summary>
        /// The logger.
        /// </summary>
        private readonly ILogger logger;

        /// <summary>
        /// The analytics tracker.
        /// </summary>
        private readonly IAnalyticsTracker analyticsTracker;

        /// <summary>
        /// The item repository.
        /// </summary>
        private readonly IItemRepository itemRepository;

        private Type baseType;
        /// <summary>
        /// Initializes a new instance of the <see cref="SimpleForm"/> class.
        /// </summary>
        public SimpleForm()
          : this(DependenciesManager.Resolve<FormDataHandler>(), DependenciesManager.Logger, DependenciesManager.AnalyticsTracker, DependenciesManager.Resolve<IItemRepository>())
        {
        }

        public SimpleForm([NotNull] FormDataHandler formDataHandler, [NotNull] ILogger logger, [NotNull] IAnalyticsTracker analyticsTracker, [NotNull] IItemRepository itemRepository) : base(formDataHandler, logger, analyticsTracker, itemRepository)
        {
            Assert.ArgumentNotNull(formDataHandler, "formDataHandler");
            Assert.ArgumentNotNull(logger, "logger");
            Assert.ArgumentNotNull(analyticsTracker, "analyticsTracker");
            Assert.ArgumentNotNull(itemRepository, "itemRepository");

            this.formDataHandler = formDataHandler;
            this.logger = logger;
            this.analyticsTracker = analyticsTracker;
            this.itemRepository = itemRepository;
            baseType = typeof(Sitecore.Form.Core.Ascx.Controls.SimpleForm);
        }
        protected override void OnClick(object sender, EventArgs e)
        {
            Assert.ArgumentNotNull(sender, "sender");

            //this.UpdateSubmitAnalytics();
            baseType.GetMethod("UpdateSubmitAnalytics", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(this, new object[]{ });
            //this.UpdateSubmitCounter();
            baseType.GetMethod("UpdateSubmitCounter", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(this, new object[] { });
            

            bool onSucceedValidation = false;
            System.Web.UI.ValidatorCollection validators = (this.Page ?? new Page()).GetValidators(((Control)sender).ID);
            if (validators.FirstOrDefault(v => !v.IsValid && v is IAttackProtection) != null)
            {
                validators.ForEach(
                  v =>
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

                var actions = new List<IActionDefinition>();
                this.CollectActions(this, actions);

                try
                {
                    this.formDataHandler.ProcessForm(this.FormID, this.GetChildState().ToArray(), actions.ToArray());

                    this.OnSuccessSubmit();

                    //   OnSucceedValidation(new EventArgs());
                    baseType.GetMethod("OnSucceedValidation", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(this, new object[] { new EventArgs() });


                   // this.OnSucceedSubmit(new EventArgs());
                    baseType.GetMethod("OnSucceedSubmit", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(this, new object[] { new EventArgs() });
                }
                catch (ThreadAbortException)
                {
                    onSucceedValidation = true;
                }
                catch (ValidatorException ex)
                {
                    this.OnRefreshError(new string[] { ex.Message });
                }
                catch (FormSubmitException ex)
                {
                    onSucceedValidation = true;
                    this.OnRefreshError(ex.Failures.Select(f => f.ErrorMessage).ToArray());
                }
                catch (FormVerificationException ex)
                {
                    try
                    {
                        var args = new SubmittedFormFailuresArgs(
                          this.FormID,
                          new[] { new ExecuteResult.Failure { ErrorMessage = ex.Message, StackTrace = ex.StackTrace, IsCustom = ex.IsCustomErrorMessage } })
                        { Database = StaticSettings.ContextDatabase.Name };

                        CorePipeline.Run("errorSubmit", args);
                        this.OnRefreshError(args.Failures.Select(f => f.ErrorMessage).ToArray());
                    }
                    catch (Exception unknown)
                    {
                        Log.Error(unknown.Message, unknown, this);
                    }

                    onSucceedValidation = true;
                }
            }
            else
            {

                //this.SetFocusOnError();
                baseType.GetMethod("SetFocusOnError", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(this, new object[] { });

            //    this.TrackValdationEvents(sender, e);
                baseType.GetMethod("TrackValdationEvents", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(this, new object[] { sender, e });

                // this.RequiredMarkerProccess(this, false);
                baseType.GetMethod("RequiredMarkerProccess", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(this, new object[] { this, false });
            }

            this.EventCounter.Value = (analyticsTracker.EventCounter + 1).ToString();

            if (onSucceedValidation)
            {
                //OnSucceedValidation(new EventArgs());
                baseType.GetMethod("OnSucceedValidation", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(this, new object[] { new EventArgs() });
            }

           // this.OnFailedSubmit(new EventArgs());
            baseType.GetMethod("OnFailedSubmit", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(this, new object[] { new EventArgs() });
        }
    }
}