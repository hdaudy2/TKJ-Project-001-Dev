using System.Collections.Generic;
using System.Threading.Tasks;
using Nop.Core.Domain.Stores;
using Nop.Core.Domain.Shipping;
using Nop.Services.Shipping.Pickup;

namespace Nop.Services.Stores
{
    /// <summary>
    /// Store service interface
    /// </summary>
    public partial interface IStoreService
    {
        /// <summary>
        /// Deletes a store
        /// </summary>
        /// <param name="store">Store</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        Task DeleteStoreAsync(Store store);

        /// <summary>
        /// Gets all stores
        /// </summary>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the stores
        /// </returns>
        Task<IList<Store>> GetAllStoresAsync();

        /// <summary>
        /// Gets a store 
        /// </summary>
        /// <param name="storeId">Store identifier</param>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the store
        /// </returns>
        Task<Store> GetStoreByIdAsync(int storeId);

        /// <summary>
        /// Inserts a store
        /// </summary>
        /// <param name="store">Store</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        Task InsertStoreAsync(Store store);

        /// <summary>
        /// Updates the store
        /// </summary>
        /// <param name="store">Store</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        Task UpdateStoreAsync(Store store);

        /// <summary>
        /// Indicates whether a store contains a specified host
        /// </summary>
        /// <param name="store">Store</param>
        /// <param name="host">Host</param>
        /// <returns>true - contains, false - no</returns>
        bool ContainsHostValue(Store store, string host);

        /// <summary>
        /// Returns a list of names of not existing stores
        /// </summary>
        /// <param name="storeIdsNames">The names and/or IDs of the store to check</param>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the list of names and/or IDs not existing stores
        /// </returns>
        Task<string[]> GetNotExistingStoresAsync(string[] storeIdsNames);

        #region Store Shipping methods
        /// <summary>
        /// Does store restriction exist
        /// </summary>
        /// <param name="shippingMethod">Shipping method</param>
        /// <param name="storeId">Store identifier</param>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the result
        /// </returns>
        Task<bool> StoreRestrictionExistsAsync(ShippingMethod shippingMethod, int storeId);

        /// <summary>
        /// Gets all StoreShippingMethod
        /// </summary>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the stores
        /// </returns>
        Task<IList<StoreShippingMethod>> GetAllStoreShippingMethodAsync();

        /// <summary>
        /// Gets all StoreShippingMethod
        /// </summary>
        /// <param name="storeId">Store identifier</param>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the stores
        /// </returns>
        Task<IList<StoreShippingMethod>> GetAllStoreShippingMethodByStoreIdAsync(int storeId);
        

        /// <summary>
        /// Gets store shipping method mappings
        /// </summary>
        /// <param name="shippingMethodId">The shipping method identifier</param>
        /// <param name="storeId">Store identifier</param>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the shipping country mappings
        /// </returns>
        Task<IList<StoreShippingMethod>> GetStoreShippingMethodAsync(int shippingMethodId, int storeId);

        /// <summary>
        /// Inserts a store shipping method mapping
        /// </summary>
        /// <param name="StoreShippingMethod">Shipping country mapping</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        Task InsertStoreShippingMethodAsync(StoreShippingMethod StoreShippingMethod);

        /// <summary>
        /// Delete the store shipping method mapping
        /// </summary>
        /// <param name="StoreShippingMethod">Shipping country mapping</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        Task DeleteStoreShippingMethodAsync(StoreShippingMethod StoreShippingMethod);
        #endregion
        
        #region Extensions by QuanNH
        Task<IList<Store>> GetStoreNameByIdAsync(int[] storeId);
        Task<IList<Store>> GetAllStoresByEntityNameAsync(int entityId, string entityName);
        #endregion
    }
}