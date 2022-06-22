#region Multi-Tenant Plugin
using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Web.Areas.Admin.Models.Stores;
using Nop.Services.Localization;
using Nop.Services.Security;
using Nop.Services.Stores;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Filters;
using Nop.Services.Customers;
using Nop.Core;
using Nop.Web.Areas.Admin.Factories;
using Nop.Web.Areas.Admin.Infrastructure.Mapper.Extensions;
using Nop.Core.Domain.Stores;
using Nop.Services.Messages;
using Nop.Core.Domain.Catalog;
using Nop.Web.Framework.Extensions;
using Nop.Web.Framework.Models.Extensions;
using System.Threading.Tasks;

namespace Nop.Web.Areas.Admin.Controllers
{
    public partial class StoreMappingController : BaseAdminController
    {
        private readonly ICustomerService _customerService;
        private readonly IStoreService _storeService;
        private readonly IStoreMappingService _storeMappingService;
        private readonly ILocalizationService _localizationService;
        private readonly IPermissionService _permissionService;
        private readonly IWorkContext _workContext;
        private readonly INotificationService _notificationService;
        private readonly IBaseAdminModelFactory _baseAdminModelFactory;
        private readonly CatalogSettings _catalogSettings;

        public StoreMappingController(ICustomerService customerService, 
            IStoreService storeService, 
            IStoreMappingService storeMappingService,
            ILocalizationService localizationService,
            IPermissionService permissionService,
            IWorkContext workContext,
            INotificationService notificationService,
            IBaseAdminModelFactory baseAdminModelFactory,
            CatalogSettings catalogSettings
            )
        {
            this._customerService = customerService;
            this._storeService = storeService;
            this._storeMappingService = storeMappingService;
            this._localizationService = localizationService;
            this._permissionService = permissionService;
            this._workContext = workContext;
            this._notificationService = notificationService;
            this._baseAdminModelFactory = baseAdminModelFactory;
            this._catalogSettings = catalogSettings;
        }

        #region Utilities

        protected virtual async Task<StoreMappingModel> PrepareModelStoresAsync(StoreMappingModel model)
        {
            //stores
            var allStores = await _storeService.GetAllStoresByEntityNameAsync((await _workContext.GetCurrentCustomerAsync()).Id, "Stores");
            if (allStores.Count <= 0)
            {
                allStores = await _storeService.GetAllStoresAsync();
                model.AvailableStores.Add(new SelectListItem() { Text = await _localizationService.GetResourceAsync("Admin.Common.All"), Value = "0" });
            }
            foreach (var s in allStores)
                model.AvailableStores.Add(new SelectListItem() { Text = s.Name, Value = s.Id.ToString() });

            //Customer
            int[] searchCustomerRoleIds = new int[] { 3 };

            foreach (var s in await _customerService.GetAllCustomersAsync(customerRoleIds: searchCustomerRoleIds))
                model.AvailableCustomers.Add(new SelectListItem() { Text = s.Email, Value = s.Id.ToString() });

            return model;

        }

        /// <summary>
        /// Prepare StoreMapping search model
        /// </summary>
        /// <param name="searchModel">StoreMapping search model</param>
        /// <returns>StoreMapping search model</returns>
        protected virtual async Task<StoreMappingSearchModel> PrepareStoreMappingSearchModelAsync(StoreMappingSearchModel searchModel)
        {
            if (searchModel == null)
                throw new ArgumentNullException(nameof(searchModel));

            //prepare available stores
            await _baseAdminModelFactory.PrepareStoresAsync(searchModel.AvailableStores);

            searchModel.HideStoresList = _catalogSettings.IgnoreStoreLimitations || searchModel.AvailableStores.SelectionIsNotPossible();

            //prepare page parameters
            searchModel.SetGridPageSize();

            return searchModel;
        }

        /// <summary>
        /// Prepare paged StoreMapping list model
        /// </summary>
        /// <param name="searchModel">StoreMapping search model</param>
        /// <returns>StoreMapping list model</returns>
        protected virtual async Task<StoreMappingListModel> PrepareStoreMappingListModel(StoreMappingSearchModel searchModel)
        {
            if (searchModel == null)
                throw new ArgumentNullException(nameof(searchModel));

            //get StoreMappings
            var storeMappings = await _storeMappingService.GetAllStoreMappingsAsync(storeId: searchModel.SearchStoreId, pageIndex: searchModel.Page - 1, pageSize: searchModel.PageSize);

            //prepare list model
            var model = await new StoreMappingListModel().PrepareToGridAsync(searchModel, storeMappings, () =>
            {
                //fill in model values from the entity
                return storeMappings.SelectAwait(async storeMapping => {
                    var _storeMapping = storeMapping.ToModel<StoreMappingModel>();
                    var detailStore = await _storeService.GetStoreByIdAsync(storeMapping.StoreId);
                    var detailCustomer = await _customerService.GetCustomerByIdAsync(storeMapping.EntityId);

                    //fill in additional values (not existing in the entity)
                    _storeMapping.UserName = detailCustomer == null ? "This user has been deleted" : detailCustomer.Email;
                    _storeMapping.StoreName = detailStore == null ? "This store has been deleted" : detailStore.Name;
                    _storeMapping.StoreUrl = detailStore == null ? "This store has been deleted" : detailStore.Url;
                    return _storeMapping;
                });
            });

            return model;
            
        }
        #endregion
        public virtual async Task<IActionResult> List()
        {
            if (!await _permissionService.AuthorizeAsync(StandardPermissionProvider.ManageStores))
                return AccessDeniedView();

            var model = await PrepareStoreMappingSearchModelAsync(new StoreMappingSearchModel());
            return View(model);
        }

        [HttpPost]
        public virtual async Task<IActionResult> List(StoreMappingSearchModel searchModel)
        {
            if (!await _permissionService.AuthorizeAsync(StandardPermissionProvider.ManageStores))
                return await AccessDeniedDataTablesJson();

            //prepare model
            var model = await PrepareStoreMappingListModel(searchModel);
            return Json(model);
        }

        public virtual async Task<IActionResult> Create()
        {

            if (!await _permissionService.AuthorizeAsync(StandardPermissionProvider.ManageStores))
                return AccessDeniedView();

            //prepare model
            var model = await PrepareModelStoresAsync(new StoreMappingModel());
            return View(model);
        }

        [HttpPost, ParameterBasedOnFormName("save-continue", "continueEditing")]
        public virtual async Task<IActionResult> Create(StoreMappingModel model, bool continueEditing)
        {
            if (!await _permissionService.AuthorizeAsync(StandardPermissionProvider.ManageStores))
                return AccessDeniedView();

            if (string.IsNullOrEmpty(model.EntityName))
                ModelState.AddModelError(string.Empty, "EntityName is required");

            if (model.EntityName != "Stores" && model.EntityName != "Admin")
                ModelState.AddModelError(string.Empty, "EntityName is Stores or Admin");

            if (model.StoreId == 0)
                ModelState.AddModelError(string.Empty, "Store is required");

            var currentStoreId = _storeMappingService.GetStoreIdByEntityId(model.EntityId, "Stores").FirstOrDefault();
            var adminStoreId = _storeMappingService.GetStoreIdByEntityId(model.EntityId, "Admin").FirstOrDefault();

            if (currentStoreId > 0 || adminStoreId>0)
            {
                ModelState.AddModelError(string.Empty, "Map the user to an existing store");
            }

            if (ModelState.IsValid)
            {
                var storeMapping = model.ToEntity<StoreMapping>();

                await _storeMappingService.Insert_Store_MappingAsync(storeMapping);

                _notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Admin.Configuration.Stores.Added"));
                return continueEditing ? RedirectToAction("Edit", new { id = storeMapping.Id }) : RedirectToAction("List");
            }
            model = await PrepareModelStoresAsync(model);
            return View(model);
        }
        public virtual async Task<IActionResult> Edit(int id)
        {
            if (!await _permissionService.AuthorizeAsync(StandardPermissionProvider.ManageStores))
                return AccessDeniedView();

            var storeMapping = await _storeMappingService.GetStoreMappingByIdAsync(id);
            if (storeMapping == null)
                //No store found with the specified id
                return RedirectToAction("List");

            var model = await PrepareModelStoresAsync(storeMapping.ToModel<StoreMappingModel>());
            return View(model);
        }

        [HttpPost, ParameterBasedOnFormName("save-continue", "continueEditing")]
        [FormValueRequired("save", "save-continue")]
        public virtual async Task<IActionResult> Edit(StoreMappingModel model, bool continueEditing)
        {
            if (!await _permissionService.AuthorizeAsync(StandardPermissionProvider.ManageStores))
                return AccessDeniedView();

            var storeMapping = await _storeMappingService.GetStoreMappingByIdAsync(model.Id);
            if (storeMapping == null)
                //No store found with the specified id
                return RedirectToAction("List");


            if (string.IsNullOrEmpty(model.EntityName))
                ModelState.AddModelError(string.Empty, "EntityName is required");

            if (model.EntityName != "Stores" && model.EntityName != "Admin")
                ModelState.AddModelError(string.Empty, "EntityName is Stores or Admin");

            if (model.StoreId == 0)
                ModelState.AddModelError(string.Empty, "Store is required");

            if (ModelState.IsValid)
            {
                storeMapping = model.ToEntity(storeMapping);
                await _storeMappingService.UpdateStoreMappingAsync(storeMapping);

                _notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Admin.Configuration.Stores.Updated"));
                return continueEditing ? RedirectToAction("Edit", new { id = storeMapping.Id }) : RedirectToAction("List");
            }

            model = await PrepareModelStoresAsync(model);
            return View(model);
        }

        [HttpPost]
        public virtual async Task<IActionResult> Delete(int id)
        {
            if (!await _permissionService.AuthorizeAsync(StandardPermissionProvider.ManageStores))
                return AccessDeniedView();

            var storeMapping = await _storeMappingService.GetStoreMappingByIdAsync(id);
            if (storeMapping == null)
                //No store found with the specified id
                return RedirectToAction("List");

            try
            {
                await _storeMappingService.DeleteStoreMappingAsync(storeMapping);
                _notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Admin.Configuration.Stores.Deleted"));
                return RedirectToAction("List");
            }
            catch (Exception exc)
            {
                _notificationService.SuccessNotification(exc.Message);
                return RedirectToAction("Edit", new { id = storeMapping.Id });
            }
        }
    }
}
#endregion;