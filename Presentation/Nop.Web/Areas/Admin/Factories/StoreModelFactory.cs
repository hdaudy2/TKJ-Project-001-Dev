﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nop.Core.Domain.Stores;
using Nop.Services.Localization;
using Nop.Services.Shipping;
using Nop.Services.Shipping.Date;
using Nop.Services.Shipping.Pickup;
using Nop.Services.Stores;
using Nop.Web.Areas.Admin.Infrastructure.Mapper.Extensions;
using Nop.Web.Areas.Admin.Models.Stores;
using Nop.Web.Areas.Admin.Models.Shipping;
using Nop.Web.Framework.Factories;
using Nop.Web.Framework.Models.Extensions;

namespace Nop.Web.Areas.Admin.Factories
{
    /// <summary>
    /// Represents the store model factory implementation
    /// </summary>
    public partial class StoreModelFactory : IStoreModelFactory
    {
        #region Fields

        private readonly IBaseAdminModelFactory _baseAdminModelFactory;
        private readonly ILocalizationService _localizationService;
        private readonly ILocalizedModelFactory _localizedModelFactory;
        private readonly IStoreService _storeService;
        private readonly IShippingService _shippingService;

        #endregion

        #region Ctor

        public StoreModelFactory(IBaseAdminModelFactory baseAdminModelFactory,
            ILocalizationService localizationService,
            ILocalizedModelFactory localizedModelFactory,
            IStoreService storeService,
            IShippingService shippingService)
        {
            _baseAdminModelFactory = baseAdminModelFactory;
            _localizationService = localizationService;
            _localizedModelFactory = localizedModelFactory;
            _storeService = storeService;
            _shippingService = shippingService;
        }

        #endregion
        
        #region Methods

        /// <summary>
        /// Prepare store search model
        /// </summary>
        /// <param name="searchModel">Store search model</param>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the store search model
        /// </returns>
        public virtual Task<StoreSearchModel> PrepareStoreSearchModelAsync(StoreSearchModel searchModel)
        {
            if (searchModel == null)
                throw new ArgumentNullException(nameof(searchModel));

            //prepare page parameters
            searchModel.SetGridPageSize();

            return Task.FromResult(searchModel);
        }

        /// <summary>
        /// Prepare paged store list model
        /// </summary>
        /// <param name="searchModel">Store search model</param>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the store list model
        /// </returns>
        public virtual async Task<StoreListModel> PrepareStoreListModelAsync(StoreSearchModel searchModel)
        {
            if (searchModel == null)
                throw new ArgumentNullException(nameof(searchModel));

            //get stores
            var stores = (await _storeService.GetAllStoresAsync()).ToPagedList(searchModel);

            //prepare list model
            var model = new StoreListModel().PrepareToGrid(searchModel, stores, () =>
            {
                //fill in model values from the entity
                return stores.Select(store => store.ToModel<StoreModel>());
            });

            return model;
        }

        /// <summary>
        /// Prepare store model
        /// </summary>
        /// <param name="model">Store model</param>
        /// <param name="store">Store</param>
        /// <param name="excludeProperties">Whether to exclude populating of some properties of model</param>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the store model
        /// </returns>
        public virtual async Task<StoreModel> PrepareStoreModelAsync(StoreModel model, Store store, bool excludeProperties = false)
        {
            Func<StoreLocalizedModel, int, Task> localizedModelConfiguration = null;

            if (store != null)
            {
                //fill in model values from the entity
                model ??= store.ToModel<StoreModel>();

                //define localized model configuration action
                localizedModelConfiguration = async (locale, languageId) =>
                {
                    locale.Name = await _localizationService.GetLocalizedAsync(store, entity => entity.Name, languageId, false, false);
                };
            }

            //prepare available languages
            await _baseAdminModelFactory.PrepareLanguagesAsync(model.AvailableLanguages, 
                defaultItemText: await _localizationService.GetResourceAsync("Admin.Common.EmptyItemText"));

            //prepare localized models
            if (!excludeProperties)
                model.Locales = await _localizedModelFactory.PrepareLocalizedModelsAsync(localizedModelConfiguration);

            return model;
        }
        #endregion
        
        #region Store Shipping methods
        /// <summary>
        /// Prepare store shipping method restriction model
        /// </summary>
        /// <param name="model">Store Shipping method restriction model</param>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the shipping method restriction model
        /// </returns>
        public virtual async Task<StoreShippingMethodRestrictionModel> PrepareStoreShippingMethodRestrictionModelAsync(StoreShippingMethodRestrictionModel model)
        {
            if (model == null)
                throw new ArgumentNullException(nameof(model));

            model.AvailableStores = await _storeService.GetAllStoresAsync();

            foreach (var shippingMethod in await _shippingService.GetAllShippingMethodsAsync())
            {
                model.AvailableShippingMethods.Add(shippingMethod.ToModel<ShippingMethodModel>());
                foreach (var store in model.AvailableStores)
                {
                    if (!model.Restricted.ContainsKey(store.Id))
                        model.Restricted[store.Id] = new Dictionary<int, bool>();

                    model.Restricted[store.Id][shippingMethod.Id] = await _storeService.StoreRestrictionExistsAsync(shippingMethod, store.Id);
                }
            }

            return model;
        }

        #endregion
    }
}