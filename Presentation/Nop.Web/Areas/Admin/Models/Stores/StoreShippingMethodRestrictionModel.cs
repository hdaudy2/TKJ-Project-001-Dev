using System.Collections.Generic;
using Nop.Core.Domain.Stores;
using Nop.Web.Areas.Admin.Models.Directory;
using Nop.Web.Areas.Admin.Models.Shipping;
using Nop.Web.Framework.Models;

namespace Nop.Web.Areas.Admin.Models.Stores
{
    /// <summary>
    /// Represents a Store shipping method restriction model
    /// </summary>
    public partial record StoreShippingMethodRestrictionModel : BaseNopModel
    {
        #region Ctor

        public StoreShippingMethodRestrictionModel()
        {
            AvailableShippingMethods = new List<ShippingMethodModel>();
            AvailableStores = new List<Store>();
            Restricted = new Dictionary<int, IDictionary<int, bool>>();
        }

        #endregion

        #region Properties

        public IList<ShippingMethodModel> AvailableShippingMethods { get; set; }

        public IList<Store> AvailableStores { get; set; }

        //[store id] / [shipping method id] / [restricted]
        public IDictionary<int, IDictionary<int, bool>> Restricted { get; set; }

        #endregion
    }
}