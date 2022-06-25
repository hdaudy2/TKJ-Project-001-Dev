using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nop.Core;
using Nop.Core.Domain.Messages;
using Nop.Data;
using Nop.Services.Stores;

namespace Nop.Services.Messages
{
    /// <summary>
    /// Email account service
    /// </summary>
    public partial class EmailAccountService : IEmailAccountService
    {
        #region Fields

        private readonly IRepository<EmailAccount> _emailAccountRepository;
        private readonly IStoreContext _storeContext;


        #endregion

        #region Ctor

        public EmailAccountService(IRepository<EmailAccount> emailAccountRepository, IStoreContext storeContext)
        {
            _emailAccountRepository = emailAccountRepository;
            _storeContext = storeContext;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Inserts an email account
        /// </summary>
        /// <param name="emailAccount">Email account</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        public virtual async Task InsertEmailAccountAsync(EmailAccount emailAccount)
        {
            var currentStore = await _storeContext.GetCurrentStoreAsync();

            if (emailAccount == null)
                throw new ArgumentNullException(nameof(emailAccount));

            emailAccount.Email = CommonHelper.EnsureNotNull(emailAccount.Email);
            emailAccount.DisplayName = CommonHelper.EnsureNotNull(emailAccount.DisplayName);
            emailAccount.Host = CommonHelper.EnsureNotNull(emailAccount.Host);
            emailAccount.Username = CommonHelper.EnsureNotNull(emailAccount.Username);
            emailAccount.Password = CommonHelper.EnsureNotNull(emailAccount.Password);

            emailAccount.Email = emailAccount.Email.Trim();
            emailAccount.DisplayName = emailAccount.DisplayName.Trim();
            emailAccount.Host = emailAccount.Host.Trim();
            emailAccount.Username = emailAccount.Username.Trim();
            emailAccount.Password = emailAccount.Password.Trim();

            emailAccount.Email = CommonHelper.EnsureMaximumLength(emailAccount.Email, 255);
            emailAccount.DisplayName = CommonHelper.EnsureMaximumLength(emailAccount.DisplayName, 255);
            emailAccount.Host = CommonHelper.EnsureMaximumLength(emailAccount.Host, 255);
            emailAccount.Username = CommonHelper.EnsureMaximumLength(emailAccount.Username, 255);
            emailAccount.Password = CommonHelper.EnsureMaximumLength(emailAccount.Password, 255);
            
            emailAccount.RegisteredInStoreId = currentStore.Id;
            emailAccount.DefaultForStore = false;

            await _emailAccountRepository.InsertAsync(emailAccount);
        }

        /// <summary>
        /// Updates an email account
        /// </summary>
        /// <param name="emailAccount">Email account</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        public virtual async Task UpdateEmailAccountAsync(EmailAccount emailAccount)
        {
            if (emailAccount == null)
                throw new ArgumentNullException(nameof(emailAccount));

            emailAccount.Email = CommonHelper.EnsureNotNull(emailAccount.Email);
            emailAccount.DisplayName = CommonHelper.EnsureNotNull(emailAccount.DisplayName);
            emailAccount.Host = CommonHelper.EnsureNotNull(emailAccount.Host);
            emailAccount.Username = CommonHelper.EnsureNotNull(emailAccount.Username);
            emailAccount.Password = CommonHelper.EnsureNotNull(emailAccount.Password);

            emailAccount.Email = emailAccount.Email.Trim();
            emailAccount.DisplayName = emailAccount.DisplayName.Trim();
            emailAccount.Host = emailAccount.Host.Trim();
            emailAccount.Username = emailAccount.Username.Trim();
            emailAccount.Password = emailAccount.Password.Trim();

            emailAccount.Email = CommonHelper.EnsureMaximumLength(emailAccount.Email, 255);
            emailAccount.DisplayName = CommonHelper.EnsureMaximumLength(emailAccount.DisplayName, 255);
            emailAccount.Host = CommonHelper.EnsureMaximumLength(emailAccount.Host, 255);
            emailAccount.Username = CommonHelper.EnsureMaximumLength(emailAccount.Username, 255);
            emailAccount.Password = CommonHelper.EnsureMaximumLength(emailAccount.Password, 255);

            await _emailAccountRepository.UpdateAsync(emailAccount);
        }

        /// <summary>
        /// Deletes an email account
        /// </summary>
        /// <param name="emailAccount">Email account</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        public virtual async Task DeleteEmailAccountAsync(EmailAccount emailAccount)
        {
            if (emailAccount == null)
                throw new ArgumentNullException(nameof(emailAccount));

            if ((await GetAllEmailAccountsAsync()).Count == 1)
                throw new NopException("You cannot delete this email account. At least one account is required.");

            await _emailAccountRepository.DeleteAsync(emailAccount);
        }

        /// <summary>
        /// Gets an email account by identifier
        /// </summary>
        /// <param name="emailAccountId">The email account identifier</param>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the email account
        /// </returns>
        public virtual async Task<EmailAccount> GetEmailAccountByIdAsync(int emailAccountId)
        {
            return await _emailAccountRepository.GetByIdAsync(emailAccountId, cache => default);
        }

        /// <summary>
        /// Gets default email account by Store identifier
        /// </summary>
        /// <param name="storeId">Store identifier</param>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the email account
        /// </returns>
        public virtual async Task<EmailAccount> GetDefaultEmailAccountByStoreIdAsync(int storeId)
        {
            var query = from ea in _emailAccountRepository.Table
                where ea.RegisteredInStoreId == storeId && ea.DefaultForStore == true
                orderby ea.Id
                select ea;
            
            return await query.FirstOrDefaultAsync();
        }

        /// <summary>
        /// Gets all email accounts
        /// </summary>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the email accounts list
        /// </returns>
        public virtual async Task<IList<EmailAccount>> GetAllEmailAccountsAsync()
        {
            var emailAccounts = await _emailAccountRepository.GetAllAsync(query =>
            {
                return from ea in query
                    orderby ea.Id
                    select ea;
            }, cache => default);

            return emailAccounts;
        }

        /// <summary>
        /// Gets all email accounts
        /// </summary>
        /// <param name="StoreId">Store identifier</param>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the email accounts list
        /// </returns>
        public virtual async Task<IList<EmailAccount>> GetAllEmailAccountsAsync(Nullable<int> StoreId)
        {
            if (StoreId == null)
                throw new ArgumentNullException(nameof(StoreId));
            
            return await _emailAccountRepository.GetAllAsync(query =>
            {
                return from ea in query
                    where ea.RegisteredInStoreId == StoreId
                    orderby ea.Id
                    select ea;
            });
        }

        #endregion
    }
}