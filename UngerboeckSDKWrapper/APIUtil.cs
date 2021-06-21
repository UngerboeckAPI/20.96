﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net.Http.Headers;
using UngerboeckSDKPackage;
using System.Net;

namespace UngerboeckSDKWrapper
{
  public class APIUtil
  {
    public static void RetrieveAPIToken(ref HttpClient USISDKClient, string userID, string password, string ungerboeckURI)
    {
      ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls; //This is added for convenience so your calls will start supporting the new TLS versions, rather than you having to update every client that uses the wrapper.

      /*Here are some important info on how the token works.
       * 
       * First, by default, it will last for 720 hours with use and 168 hours unused.  
       * Starting in 20.95, you can shorten this time by using the header "ExpiresInHours" with an integer value 1 to 720 during the call to SDK_INITIALIZE.
       * This will decrease its lifespan for increased security.  
       * 
       * USISDKClient.DefaultRequestHeaders.Add("ExpiresInHours", "1");
       * 
       * Secondly, you do not need to initialize the token for every call.  
       * It's best practice to retrieve a token once, then proceed to do API calls until before or when the token expires to lower the amount of calls and latency.*/

      APIUtil.InitializeAPIClient(USISDKClient, ungerboeckURI, userID, password);

      // Run an async/await call to the ungerboeck API.  You can also use a normal http call if you wish, for all API interactions.
      Task<string> ungerboeckSDKInitializationTask = GetAsync<string>(USISDKClient, "sdk_initialize"); //If you use async/await, you can use a task based asynchonous call

      if ((ungerboeckSDKInitializationTask == null))
      {
        throw new Exception("UngerboeckSDKInitialization is nothing"); // This likely will never happen
      }

      string ungerboeckSDKToken = ungerboeckSDKInitializationTask.Result;
      USISDKClient.DefaultRequestHeaders.Add("Token", ungerboeckSDKToken); // Attach the newly received token 
      USISDKClient.DefaultRequestHeaders.Remove("Authorization");


    }

    private static void InitializeAPIClient(HttpClient USISDKClient, string astrDomain, string astrUngerboeckUser, string astrUngerboeckPassword)
    {
      USISDKClient.BaseAddress = new Uri(astrDomain); // Base address of the Ungerboeck server
      USISDKClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(
          ASCIIEncoding.ASCII.GetBytes($"{astrUngerboeckUser}:{astrUngerboeckPassword}"))); // myUserID:myPassword converted to a Base64String

      USISDKClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }


    ///<summary>
    ///Use this wrapper for doing searches in the Ungerboeck API.  T should be in the form of an IEnummerable(of x) where x is an object of the UngerboeckSDKPackage.Model classes (ex: IEnummerable(of UngerboeckSDKPackage.EventsModel)
    ///</summary>
    ///<typeparam name="T">This should be in the form of an IEnummerable(of x) where x is an object of the UngerboeckSDKPackage.Model classes (ex: IEnummerable(of UngerboeckSDKPackage.EventsModel))</typeparam>
    ///<param name="USISDKClient">This is the HTTPClient object used in all SDK Wrapper classes.</param>
    ///<param name="searchMetadata">This contains info on the results of the search, found in the response header.  It's pass by reference, so just include the empty UngerboeckSDKPackage.SearchMetadataModel.</param>
    ///<param name="orgCode">The Ungerboeck Organization Code where you are searching</param>
    ///<param name="searchOData">This is a filter in the form of <a href="https://supportcenter.ungerboeck.com/hc/en-us/articles/115010610608-Searching-Using-the-API">OData</a>.  If you want all results, use 'All' as the string value.</param>
    ///<param name="orderBy">Optional.  In this operator, you list the properties you want to order your search by, also qualifying it with desc if you want to have that property sort descending. Each property added is sorted ascending by default.  Multiple levels of sorting are achieved by comma-delimited property names. (Example: "PostalCode desc, FirstName")</param>
    ///<param name="pageSize">Optional.  This defaults to 1000, but you can configure the result page set to be anything.</param>
    ///<param name="maxResults">Optional.  This will limit the total amount of results returned, before the result set is cached for paging purposes.  It's useful for performance problems.  This defaults to 100000, but you can configure this to be anything.  If the return amount exceeds this number, an error will return, signaling that you should make your query more specific.  Setting it high is useful for exports.</param>
    ///<param name="selectProperties">Optional.  You can insert a comma delimited list of properties you need to be returned in each model.  You will always get the primary keys of the model.</param>
    ///<returns></returns>
    public static IEnumerable<T> GetSearchList<T>(HttpClient USISDKClient, ref UngerboeckSDKPackage.SearchMetadataModel searchMetadata, string orgCode, string searchOData, string orderBy = "", int pageSize = 1000, int maxResults = 100000, List<string> selectProperties = null)
    {
      string orderByURLParameter, selectURLParameter;

      if (string.IsNullOrWhiteSpace(searchOData))
      {
        throw new Exception("searchOData parameter is empty.  Please set it to the desired filter using OData syntax.  If you want" +
            " all results, use \'All\' as the string value.");
      }

      string objectName = GetObjectName<T>();
      if (string.IsNullOrWhiteSpace(orderBy))
      {
        orderByURLParameter = string.Empty;
      }
      else
      {
        orderByURLParameter = "$orderby=" + orderBy;
      }

      if (selectProperties == null || selectProperties.Count <= 0)
      {
        selectURLParameter = string.Empty;
      }
      else
      {
        selectURLParameter = "$select=" + String.Join(",", selectProperties.ToArray());
      }

      string getListURL = $"{objectName}/{orgCode}?search={searchOData}$page_size={pageSize}$maxresults={maxResults}{orderByURLParameter}{selectURLParameter}"; // Pagesize, maxresults and orderby are optional.
      Task<Tuple<IEnumerable<T>, UngerboeckSDKPackage.SearchMetadataModel>> searchResultsTask = AwaitGetSearchList<T>(USISDKClient, $"{USISDKClient.BaseAddress}/api/v1/{getListURL}");
      var searchResult = searchResultsTask.Result; // Retrieve the search result tuple from the task
      searchMetadata = searchResult.Item2;

      return searchResult.Item1;
    }

    private static string GetObjectName<T>()
    {
      Type modelType = typeof(T);
      if (modelType == null || modelType.Namespace == null || modelType.Namespace != "UngerboeckSDKPackage")
      {
        throw new Exception("T should be an object of the UngerboeckSDKPackage.Model classes (ex: UngerboeckSDKPackage.EventsModel)");
      }

      return APIUtil.GetObjectNameFromModelType(modelType);
    }

    private static string GetObjectNameFromModelType(Type type)
    {
      //Almost always, the name of the Ungerboeck object is the same as the model

      if (type == typeof(UngerboeckSDKPackage.AllAccountsModel))
        return "Accounts";
      else if (type == typeof(UngerboeckSDKPackage.AccountsProductsAndServicesModel))
        return "AccountProductsAndServices";
      else
        return type.Name.Substring(0, type.Name.LastIndexOf("Model"));
    }

    private static async Task<Tuple<IEnumerable<T>, UngerboeckSDKPackage.SearchMetadataModel>> AwaitGetSearchList<T>(HttpClient USISDKClient, string searchURL)
    {
      // Run a GetAsync on the URL given by the Initialization, concatenated with the RESTFUL operation
      HttpResponseMessage response = await USISDKClient.GetAsync(searchURL).ConfigureAwait(false);

      if (APIUtil.SuccessResponse(response))
      {
        IEnumerable<T> list = await response.Content.ReadAsAsync<IEnumerable<T>>();  // Read back the item from the response
        string searchMetadata = response.Headers.GetValues("X-SearchMetadata").FirstOrDefault();
        SearchMetadataModel searchData = Newtonsoft.Json.JsonConvert.DeserializeObject<UngerboeckSDKPackage.SearchMetadataModel>(searchMetadata);
        return new Tuple<IEnumerable<T>, UngerboeckSDKPackage.SearchMetadataModel>(list, searchData);
      }

      return null;
    }

    ///<summary>
    ///Use this function to navigate through fetched search results.  T should be in the form of an IEnummerable(of x) where x is an object of the UngerboeckSDKPackage.Model classes (ex: IEnummerable(of UngerboeckSDKPackage.EventsModel)
    ///</summary>
    ///<typeparam name="T">This should be in the form of an IEnummerable(of x) where x is an object of the UngerboeckSDKPackage.Model classes (ex: IEnummerable(of UngerboeckSDKPackage.EventsModel))</typeparam>
    ///<param name="USISDKClient">This is the HTTPClient object used in all SDK Wrapper classes.</param>
    ///<param name="searchMetadata">This contains info on the results of the search, returned in the original search response header.  It's pass by reference, so just include the same UngerboeckSDKPackage.SearchMetadataModel you got from the original search.</param>
    ///<param name="navigationURL">This should be one of the URLs found in the UngerboeckSDKPackage.SearchMetadataModel.Links class.  Use the full URL of where you want to navigate to (Ex: https://mywebsite/api/v1/Bookings/10?search=APISearch|5e9184e0-782c-4deb-ae17-e37927281ca0$page=2$page_size=10)</param>
    ///<returns>A list of models</returns>
    public static IEnumerable<T> NavigateSearchList<T>(HttpClient USISDKClient, ref UngerboeckSDKPackage.SearchMetadataModel searchMetadata, string navigationURL)
    {
      Task<Tuple<IEnumerable<T>, UngerboeckSDKPackage.SearchMetadataModel>> searchResultsTask = AwaitGetSearchList<T>(USISDKClient, navigationURL);

      var searchResult = searchResultsTask.Result; // Retrieve the search result tuple from the task
      searchMetadata = searchResult.Item2;
      return searchResult.Item1;
    }

    /// <summary>
    /// If the object supports it, you can do multiple save operations in one transaction.  See the API Help sandbox for a list of what has Bulk.
    /// </summary>
    /// <typeparam name="T">Set the UngerboeckSDKPackage.UngerboeckModel type to pick the object of the bulk operation</typeparam>
    /// <param name="uSISDKClient"></param>
    /// <param name="bulkRequestModel">This contains the list of bulk items, as well as the choice to continue on failure of a save.</param>
    /// <returns></returns>
    public static BulkResponseModel DoBulk<T>(HttpClient USISDKClient, BulkRequestModel bulkRequestModel)
    {
      string objectName = GetObjectName<T>();

      string getListURL = $"Bulk/{objectName}";

      Task<UngerboeckSDKPackage.BulkResponseModel> bulkResponseModelTask = PostAsync<BulkRequestModel, BulkResponseModel>(USISDKClient, getListURL, bulkRequestModel);
      return bulkResponseModelTask.Result;
    }

    private static async Task<T> GetAsync<T>(HttpClient USISDKClient, string URL)
    {
      HttpResponseMessage response = await USISDKClient.GetAsync($"{USISDKClient.BaseAddress}/api/v1/{URL}").ConfigureAwait(false);
      if (SuccessResponse(response))
      {
        T Model = await response.Content.ReadAsAsync<T>();
        return Model;
      }

      return default(T);
    }

    private static async Task<string> GetStringAsync(HttpClient USISDKClient, string URL)
    {
      //This is needed for Documents Download, which comes in as an octet and isn't compatible with ReadAsAsync

      HttpResponseMessage response = await USISDKClient.GetAsync($"{USISDKClient.BaseAddress}/api/v1/{URL}").ConfigureAwait(false);
      if (SuccessResponse(response))
      {
        string Model = await response.Content.ReadAsStringAsync();
        return Model;
      }

      return null;
    }

    private static async Task<T> PostAsync<T>(HttpClient USISDKClient, string URL, T item)
    {
      var content = new StringContent(JsonConvert.SerializeObject(item), Encoding.UTF8, "application/json");
      var response = await USISDKClient.PostAsync($"{USISDKClient.BaseAddress}/api/v1/{URL}", content).ConfigureAwait(false);
      if (SuccessResponse(response))
      {
        return await response.Content.ReadAsAsync<T>();
      }

      return default(T);
    }

    private static async Task<K> PostAsync<T, K>(HttpClient USISDKClient, string URL, T item)
    {
      var content = new StringContent(JsonConvert.SerializeObject(item), Encoding.UTF8, "application/json");
      var response = await USISDKClient.PostAsync($"{USISDKClient.BaseAddress}/api/v1/{URL}", content).ConfigureAwait(false);
      if (SuccessResponse(response))
      {
        return await response.Content.ReadAsAsync<K>();
      }

      return default(K);
    }

    private static async Task<T> PutAsync<T>(HttpClient USISDKClient, string URL, T item)
    {
      HttpResponseMessage response = await USISDKClient.PutAsJsonAsync($"{USISDKClient.BaseAddress}/api/v1/{URL}", item).ConfigureAwait(false);
      if (APIUtil.SuccessResponse(response))
      {
        var updatedModel = await response.Content.ReadAsAsync<T>();
        return updatedModel;
      }

      return default(T);
    }

    private static async Task<K> PutAsync<T, K>(HttpClient USISDKClient, string URL, T item)
    {
      HttpResponseMessage response = await USISDKClient.PutAsJsonAsync($"{USISDKClient.BaseAddress}/api/v1/{URL}", item).ConfigureAwait(false);
      if (APIUtil.SuccessResponse(response))
      {
        var updatedModel = await response.Content.ReadAsAsync<K>();
        return updatedModel;
      }

      return default(K);
    }

    private static async Task<T> PutAsyncBulk<T, U>(HttpClient USISDKClient, string URL, U item)
    {
      HttpResponseMessage response = await USISDKClient.PutAsJsonAsync($"{USISDKClient.BaseAddress}/api/v1/{URL}", item).ConfigureAwait(false);
      if (APIUtil.SuccessResponse(response))
      {
        var updatedModel = await response.Content.ReadAsAsync<T>();
        return updatedModel;
      }

      return default(T);
    }

    private static async Task<T> DeleteAsync<T>(HttpClient USISDKClient, string URL)
    {
      HttpResponseMessage response = await USISDKClient.DeleteAsync($"{USISDKClient.BaseAddress}/api/v1/{URL}").ConfigureAwait(false);
      if (APIUtil.SuccessResponse(response))
      {
        var updatedModel = await response.Content.ReadAsAsync<T>();
        return updatedModel;
      }

      return default(T);
    }

    public static UngerboeckSDKPackage.AllAccountsModel GetAccount(HttpClient USISDKClient, string orgCode, string accountCode)
    {
      Task<UngerboeckSDKPackage.AllAccountsModel> accountTask = GetAsync<UngerboeckSDKPackage.AllAccountsModel>(USISDKClient, $"Accounts/{orgCode}/{accountCode}");
      return accountTask.Result;
    }

    public static UngerboeckSDKPackage.AllAccountsModel AddAccount(HttpClient USISDKClient, UngerboeckSDKPackage.AllAccountsModel account)
    {
      Task<UngerboeckSDKPackage.AllAccountsModel> accountTask = PostAsync(USISDKClient, "Accounts", account);
      return accountTask.Result;
    }

    public static UngerboeckSDKPackage.AllAccountsModel UpdateAccount(HttpClient USISDKClient, UngerboeckSDKPackage.AllAccountsModel account)
    {
      Task<UngerboeckSDKPackage.AllAccountsModel> accountTask = PutAsync(USISDKClient, $"Accounts/{account.Organization}/{account.AccountCode}", account);
      return accountTask.Result;
    }

    public static UngerboeckSDKPackage.AllAccountsModel AddAccountWithDuplicateCheck(HttpClient USISDKClient, UngerboeckSDKPackage.AllAccountsModel account)
    {
      //Add a header with this sequence to enable duplicate check validation within ungerboeck.
      AddValidationOverride(USISDKClient, 1019);

      var accountResult = AddAccount(USISDKClient, account);
      return accountResult;
    }

    public static UngerboeckSDKPackage.AccountAffiliationsModel GetAccountAffiliation(HttpClient USISDKClient, string astrOrgCode, string astrAccountCode, string astrAffiliation)
    {
      Task<UngerboeckSDKPackage.AccountAffiliationsModel> accountAffiliationTask =
          GetAsync<UngerboeckSDKPackage.AccountAffiliationsModel>(USISDKClient, $"AccountAffiliations/{astrOrgCode}/{astrAccountCode}/{astrAffiliation}");
      return accountAffiliationTask.Result;
    }

    public static UngerboeckSDKPackage.AccountAffiliationsModel AddAffiliation(HttpClient USISDKClient, UngerboeckSDKPackage.AccountAffiliationsModel affiliation)
    {
      Task<UngerboeckSDKPackage.AccountAffiliationsModel> affiliationTask = PostAsync(USISDKClient, "AccountAffiliations", affiliation);
      return affiliationTask.Result;
    }

    public static async Task AwaitDeleteAccountAffiliation(HttpClient USISDKClient, string astrOrgCode, string astrAccountCode, string astrAffiliationCode)
    {
      HttpResponseMessage response = await USISDKClient.DeleteAsync(
          $"{USISDKClient.BaseAddress}/api/v1/AccountAffiliations/{astrOrgCode}/{astrAccountCode}/{astrAffiliationCode}").ConfigureAwait(false);
      APIUtil.SuccessResponse(response);
    }

    public static UngerboeckSDKPackage.AccountMailingListsModel GetAccountMailingList(HttpClient USISDKClient, string astrOrgCode, int aintID)
    {
      Task<UngerboeckSDKPackage.AccountMailingListsModel> accountMailingListsTask =
          GetAsync<UngerboeckSDKPackage.AccountMailingListsModel>(USISDKClient, $"AccountMailingLists/{astrOrgCode}/{aintID}");
      return accountMailingListsTask.Result;
    }
    [Obsolete("Use GetAccountMailingList to get a single item or GetSearchList for a list")]
    public static UngerboeckSDKPackage.AccountMailingListsModel GetAccountMailingLists(HttpClient USISDKClient, string astrOrgCode, int aintID)
    {
      return GetAccountMailingList(USISDKClient, astrOrgCode, aintID);
    }
    public static UngerboeckSDKPackage.AccountsReceivableVouchersModel GetAccountsReceivableVoucher(HttpClient USISDKClient, string astrOrgCode, int aintVoucherSequence)
    {
      Task<UngerboeckSDKPackage.AccountsReceivableVouchersModel> accountsReceivableVouchersTask =
          GetAsync<UngerboeckSDKPackage.AccountsReceivableVouchersModel>(USISDKClient, $"AccountsReceivableVouchers/{astrOrgCode}/{aintVoucherSequence}");
      return accountsReceivableVouchersTask.Result;
    }
    [Obsolete("Use GetAccountsReceivableVoucher to get a single item or GetSearchList for a list")]
    public static UngerboeckSDKPackage.AccountsReceivableVouchersModel GetAccountsReceivableVouchers(HttpClient USISDKClient, string astrOrgCode, int aintVoucherSequence)
    {
      return GetAccountsReceivableVoucher(USISDKClient, astrOrgCode, aintVoucherSequence);
    }
    public static UngerboeckSDKPackage.AccountTypesModel GetAccountType(HttpClient USISDKClient, string astrOrgCode, string astrCode)
    {
      Task<UngerboeckSDKPackage.AccountTypesModel> accountTypesTask =
          GetAsync<UngerboeckSDKPackage.AccountTypesModel>(USISDKClient, $"AccountTypes/{astrOrgCode}/{astrCode}");
      return accountTypesTask.Result;
    }
    [Obsolete("Use GetAccountType to get a single item or GetSearchList for a list")]
    public static UngerboeckSDKPackage.AccountTypesModel GetAccountTypes(HttpClient USISDKClient, string astrOrgCode, string astrCode)
    {
      Task<UngerboeckSDKPackage.AccountTypesModel> accountTypesTask =
          GetAsync<UngerboeckSDKPackage.AccountTypesModel>(USISDKClient, $"AccountTypes/{astrOrgCode}/{astrCode}");
      return accountTypesTask.Result;
    }
    public static UngerboeckSDKPackage.AffiliationsModel GetAffiliation(HttpClient USISDKClient, string astrOrgCode, string astrAffiliationCode)
    {
      Task<UngerboeckSDKPackage.AffiliationsModel> affiliationsTask =
          GetAsync<UngerboeckSDKPackage.AffiliationsModel>(USISDKClient, $"Affiliations/{astrOrgCode}/{astrAffiliationCode}");
      return affiliationsTask.Result;
    }
    [Obsolete("Use GetAffiliation to get a single item or GetSearchList for a list")]
    public static UngerboeckSDKPackage.AffiliationsModel GetAffiliations(HttpClient USISDKClient, string astrOrgCode, string astrAffiliationCode)
    {
      return GetAffiliation(USISDKClient, astrOrgCode, astrAffiliationCode);
    }
    public static UngerboeckSDKPackage.AlternateAddressesModel GetAlternateAddress(HttpClient USISDKClient, string astrOrgCode, string astrAccount, int aintSequenceNumber, string astrRecordType)
    {
      Task<UngerboeckSDKPackage.AlternateAddressesModel> alternateAddressesTask =
          GetAsync<UngerboeckSDKPackage.AlternateAddressesModel>(USISDKClient, $"AlternateAddresses/{astrOrgCode}/{astrAccount}/{aintSequenceNumber}/{astrRecordType}");
      return alternateAddressesTask.Result;
    }
    [Obsolete("Use GetAlternateAddress to get a single item or GetSearchList for a list")]
    public static UngerboeckSDKPackage.AlternateAddressesModel GetAlternateAddresses(HttpClient USISDKClient, string astrOrgCode, string astrAccount, int aintSequenceNumber, string astrRecordType)
    {
      Task<UngerboeckSDKPackage.AlternateAddressesModel> alternateAddressesTask =
          GetAsync<UngerboeckSDKPackage.AlternateAddressesModel>(USISDKClient, $"AlternateAddresses/{astrOrgCode}/{astrAccount}/{aintSequenceNumber}/{astrRecordType}");
      return alternateAddressesTask.Result;
    }
    public static UngerboeckSDKPackage.APDemographicsModel GetAPDemographic(HttpClient USISDKClient, string astrOrgCode, string astrSupplier)
    {
      Task<UngerboeckSDKPackage.APDemographicsModel> aPDemographicsTask =
          GetAsync<UngerboeckSDKPackage.APDemographicsModel>(USISDKClient, $"APDemographics/{astrOrgCode}/{astrSupplier}");
      return aPDemographicsTask.Result;
    }
    [Obsolete("Use GetAPDemographic to get a single item or GetSearchList for a list")]
    public static UngerboeckSDKPackage.APDemographicsModel GetAPDemographics(HttpClient USISDKClient, string astrOrgCode, string astrSupplier)
    {
      return GetAPDemographic(USISDKClient, astrOrgCode, astrSupplier);
    }
    public static UngerboeckSDKPackage.ARDemographicsModel GetARDemographic(HttpClient USISDKClient, string astrOrgCode, string astrAccount)
    {
      Task<UngerboeckSDKPackage.ARDemographicsModel> aRDemographicsTask =
          GetAsync<UngerboeckSDKPackage.ARDemographicsModel>(USISDKClient, $"ARDemographics/{astrOrgCode}/{astrAccount}");
      return aRDemographicsTask.Result;
    }
    public static ARDemographicsModel AddARDemographic(HttpClient USISDKClient, ARDemographicsModel aRDemographics)
    {
      Task<ARDemographicsModel> aRDemographicsTask = PostAsync(USISDKClient, "ARDemographics", aRDemographics);
      return aRDemographicsTask.Result;
    }
    public static ARDemographicsModel UpdateARDemographic(HttpClient USISDKClient, ARDemographicsModel aRDemographics)
    {
      Task<ARDemographicsModel> aRDemographicsTask = PutAsync(USISDKClient, $"ARDemographics/{aRDemographics.Organization}/{aRDemographics.Account}", aRDemographics);
      return aRDemographicsTask.Result;
    }
    public static async Task AwaitDeleteARDemographic(HttpClient USISDKClient, string orgCode, string account)
    {
      HttpResponseMessage response = await USISDKClient.DeleteAsync(
          $"{USISDKClient.BaseAddress}/api/v1/ARDemographics/{orgCode}/{account}"
        ).ConfigureAwait(false);

      SuccessResponse(response);
    }

    [Obsolete("Use GetARDemographic to get a single item or GetSearchList for a list")]
    public static UngerboeckSDKPackage.ARDemographicsModel GetARDemographics(HttpClient USISDKClient, string astrOrgCode, string astrAccount)
    {

      return GetARDemographic(USISDKClient, astrOrgCode, astrAccount);
    }
    public static UngerboeckSDKPackage.AccountsProductsAndServicesModel GetAccountProductService(HttpClient USISDKClient, string astrOrgCode, string astrAccountCode, string astrProductServiceCode)
    {
      Task<UngerboeckSDKPackage.AccountsProductsAndServicesModel> accountProductServiceTask =
          GetAsync<UngerboeckSDKPackage.AccountsProductsAndServicesModel>(USISDKClient, $"AccountProductsAndServices/{astrOrgCode}/{astrAccountCode}/{astrProductServiceCode}");
      return accountProductServiceTask.Result;
    }

    public static UngerboeckSDKPackage.AccountsProductsAndServicesModel AddProductService(HttpClient USISDKClient, UngerboeckSDKPackage.AccountsProductsAndServicesModel productService)
    {
      Task<UngerboeckSDKPackage.AccountsProductsAndServicesModel> ProductServiceTask = PostAsync(USISDKClient, "AccountProductsAndServices", productService);
      return ProductServiceTask.Result;
    }

    public static async Task AwaitDeleteAccountProductService(HttpClient USISDKClient, string astrOrgCode, string astrAccountCode, string astrProductServiceCode)
    {
      HttpResponseMessage response = await USISDKClient.DeleteAsync(
          $"{USISDKClient.BaseAddress}/api/v1/AccountProductsAndServices/{astrOrgCode}/{astrAccountCode}/{astrProductServiceCode}").ConfigureAwait(false);
      APIUtil.SuccessResponse(response);
    }

    public static UngerboeckSDKPackage.ActivitiesModel GetActivity(HttpClient USISDKClient, string astrOrgCode, string astrAccountCode, int aintSequenceNumber)
    {
      Task<UngerboeckSDKPackage.ActivitiesModel> activity = GetAsync<UngerboeckSDKPackage.ActivitiesModel>(USISDKClient,
          $"Activities/{astrOrgCode}/{astrAccountCode}/{aintSequenceNumber}");
      return activity.Result;
    }

    public static UngerboeckSDKPackage.ActivitiesModel AddActivity(HttpClient USISDKClient, UngerboeckSDKPackage.ActivitiesModel activity)
    {
      Task<UngerboeckSDKPackage.ActivitiesModel> activityTask = PostAsync(USISDKClient, "Activities", activity);
      return activityTask.Result;
    }

    public static UngerboeckSDKPackage.ActivitiesModel UpdateActivity(HttpClient USISDKClient, UngerboeckSDKPackage.ActivitiesModel activity)
    {
      Task<UngerboeckSDKPackage.ActivitiesModel> activityTask = PutAsync(USISDKClient,
          $"Activities/{activity.OrganizationCode}/{activity.Account}/{activity.SequenceNumber}", activity);
      return activityTask.Result;
    }
    public static async Task AwaitDeleteActivities(HttpClient USISDKClient, string astrOrgCode, string astrAccountCode, int aintSequenceNumber)
    {
      HttpResponseMessage response = await USISDKClient.DeleteAsync(
          $"{USISDKClient.BaseAddress}/api/v1/Activities/{astrOrgCode}/{astrAccountCode}/{aintSequenceNumber}").ConfigureAwait(false);
      APIUtil.SuccessResponse(response);
    }

    public static UngerboeckSDKPackage.BookingsModel AddBooking(HttpClient USISDKClient, UngerboeckSDKPackage.BookingsModel booking)
    {
      Task<UngerboeckSDKPackage.BookingsModel> bookingTask = PostAsync(USISDKClient, "Bookings", booking);
      return bookingTask.Result;
    }

    public static UngerboeckSDKPackage.BookingsModel AddBookingWithoutConflictCheck(HttpClient USISDKClient, UngerboeckSDKPackage.BookingsModel booking)
    {
      AddValidationOverride(USISDKClient, 12015);
      var bookingResult = AddBooking(USISDKClient, booking);
      return bookingResult;
    }

    public static UngerboeckSDKPackage.BookingsModel GetBooking(HttpClient USISDKClient, string astrOrgCode, int aintEventID, int aintSequenceNumber)
    {
      Task<UngerboeckSDKPackage.BookingsModel> bookingTask =
          GetAsync<UngerboeckSDKPackage.BookingsModel>(USISDKClient, $"Bookings/{astrOrgCode}/{aintEventID}/{aintSequenceNumber}");
      return bookingTask.Result;
    }

    public static UngerboeckSDKPackage.BookingsModel UpdateBooking(HttpClient USISDKClient, UngerboeckSDKPackage.BookingsModel booking)
    {
      Task<UngerboeckSDKPackage.BookingsModel> bookingTask = PutAsync(USISDKClient, $"Bookings/{booking.OrganizationCode}/{booking.Event}/{booking.SequenceNumber}", booking);
      return bookingTask.Result;
    }

    public static UngerboeckSDKPackage.BookingsModel UpdateBookingWithoutConflictCheck(HttpClient USISDKClient, UngerboeckSDKPackage.BookingsModel booking)
    {
      AddValidationOverride(USISDKClient, 12015);
      var bookingResult = UpdateBooking(USISDKClient, booking);
      return bookingResult;
    }

    public static UngerboeckSDKPackage.BoothsModel GetBooth(HttpClient USISDKClient, string astrOrgCode, int aintSequenceNumber)
    {
      Task<UngerboeckSDKPackage.BoothsModel> boothsTask =
          GetAsync<UngerboeckSDKPackage.BoothsModel>(USISDKClient, $"Booths/{astrOrgCode}/{aintSequenceNumber}");
      return boothsTask.Result;
    }
    [Obsolete("Use GetBooth to get a single item or GetSearchList for a list")]
    public static UngerboeckSDKPackage.BoothsModel GetBooths(HttpClient USISDKClient, string astrOrgCode, int aintSequenceNumber)
    {
      return GetBooth(USISDKClient, astrOrgCode, aintSequenceNumber);
    }

    public static UngerboeckSDKPackage.BoothsModel AddBooth(HttpClient USISDKClient, UngerboeckSDKPackage.BoothsModel booth)
    {
      Task<UngerboeckSDKPackage.BoothsModel> BoothTask = PostAsync(USISDKClient, $"Booths/{booth.OrganizationCode}/{booth.SequenceNumber}", booth);
      return BoothTask.Result;
    }

    public static UngerboeckSDKPackage.BoothsModel UpdateBooth(HttpClient USISDKClient, UngerboeckSDKPackage.BoothsModel booth)
    {
      Task<UngerboeckSDKPackage.BoothsModel> BoothTask = PutAsync(USISDKClient, $"Booths/{booth.OrganizationCode}/{booth.SequenceNumber}", booth);
      return BoothTask.Result;
    }

    public static async Task AwaitDeleteBooth(HttpClient USISDKClient, string orgCode, int seqNumber)
    {
      HttpResponseMessage response = await USISDKClient.DeleteAsync(
          $"{USISDKClient.BaseAddress}/api/v1/Booths/{orgCode}/{seqNumber}"
        ).ConfigureAwait(false);

      SuccessResponse(response);
    }

    public static UngerboeckSDKPackage.BulletinApprovalModel GetBulletinApproval(HttpClient USISDKClient, string astrOrgCode, int aintMeetingSequenceNumber, int aintBulletinSequenceNumber, int aintSequenceNumber, string astrBulletinFileID)
    {
      Task<UngerboeckSDKPackage.BulletinApprovalModel> bulletinApprovalTask =
          GetAsync<UngerboeckSDKPackage.BulletinApprovalModel>(USISDKClient, $"BulletinApproval/{astrOrgCode}/{aintMeetingSequenceNumber}/{aintBulletinSequenceNumber}/{aintSequenceNumber}/{astrBulletinFileID}");
      return bulletinApprovalTask.Result;
    }
    public static UngerboeckSDKPackage.BulletinsModel GetBulletin(HttpClient USISDKClient, string astrOrgCode, string astrBulletinApplication, int aintMeeting, int aintBulletin)
    {
      Task<UngerboeckSDKPackage.BulletinsModel> bulletinsTask =
          GetAsync<UngerboeckSDKPackage.BulletinsModel>(USISDKClient, $"Bulletins/{astrOrgCode}/{astrBulletinApplication}/{aintMeeting}/{aintBulletin}");
      return bulletinsTask.Result;
    }
    [Obsolete("Use GetBulletin to get a single item or GetSearchList for a list")]
    public static UngerboeckSDKPackage.BulletinsModel GetBulletins(HttpClient USISDKClient, string astrOrgCode, string astrBulletinApplication, int aintMeeting, int aintBulletin)
    {
      return GetBulletin(USISDKClient, astrOrgCode, astrBulletinApplication, aintMeeting, aintBulletin);
    }
    public static UngerboeckSDKPackage.CampaignsModel GetCampaign(HttpClient USISDKClient, string astrOrgCode, string astrID, string astrDesignation)
    {
      Task<UngerboeckSDKPackage.CampaignsModel> campaignsTask =
          GetAsync<UngerboeckSDKPackage.CampaignsModel>(USISDKClient, $"Campaigns/{astrOrgCode}/{astrID}/{astrDesignation}");
      return campaignsTask.Result;
    }
    [Obsolete("Use GetCampaign to get a single item or GetSearchList for a list")]
    public static UngerboeckSDKPackage.CampaignsModel GetCampaigns(HttpClient USISDKClient, string astrOrgCode, string astrID, string astrDesignation)
    {
      return GetCampaign(USISDKClient, astrOrgCode, astrID, astrDesignation);
    }

    public static UngerboeckSDKPackage.CampaignDetailsModel GetCampaignDetail(HttpClient USISDKClient, string astrOrgCode, string astrCampaignDesignation, string astrCampaign, int aintSequenceNumber)
    {
      Task<UngerboeckSDKPackage.CampaignDetailsModel> campaignDetailsTask =
          GetAsync<UngerboeckSDKPackage.CampaignDetailsModel>(USISDKClient, $"CampaignDetails/{astrOrgCode}/{astrCampaignDesignation}/{astrCampaign}/{aintSequenceNumber}");
      return campaignDetailsTask.Result;
    }
    public static UngerboeckSDKPackage.ConcessionsModel GetConcession(HttpClient USISDKClient, string astrOrgCode, int aintSequenceNumber)
    {
      Task<UngerboeckSDKPackage.ConcessionsModel> concessionTask =
          GetAsync<UngerboeckSDKPackage.ConcessionsModel>(USISDKClient, $"Concessions/{astrOrgCode}/{aintSequenceNumber}");
      return concessionTask.Result;
    }
    [Obsolete("Use GetConcession to get a single item or GetSearchList for a list")]
    public static UngerboeckSDKPackage.ConcessionsModel GetConcessions(HttpClient USISDKClient, string astrOrgCode, int aintSequenceNumber)
    {
      Task<UngerboeckSDKPackage.ConcessionsModel> concessionTask =
          GetAsync<UngerboeckSDKPackage.ConcessionsModel>(USISDKClient, $"Concessions/{astrOrgCode}/{aintSequenceNumber}");
      return concessionTask.Result;
    }
    public static UngerboeckSDKPackage.ContractsModel GetContract(HttpClient USISDKClient, string astrOrgCode, int aintSequence)
    {
      Task<UngerboeckSDKPackage.ContractsModel> contractTask =
          GetAsync<UngerboeckSDKPackage.ContractsModel>(USISDKClient, $"Contracts/{astrOrgCode}/{aintSequence}");
      return contractTask.Result;
    }
    [Obsolete("Use GetContract to get a single item or GetSearchList for a list")]
    public static UngerboeckSDKPackage.ContractsModel GetContracts(HttpClient USISDKClient, string astrOrgCode, int aintSequence)
    {
      Task<UngerboeckSDKPackage.ContractsModel> contractTask =
          GetAsync<UngerboeckSDKPackage.ContractsModel>(USISDKClient, $"Contracts/{astrOrgCode}/{aintSequence}");
      return contractTask.Result;
    }
    public static UngerboeckSDKPackage.CountriesModel GetCountry(HttpClient USISDKClient, string astrOrgCode, string astrCode)
    {
      Task<UngerboeckSDKPackage.CountriesModel> countryTask;
      if (astrCode == "***")
      {
        //Due to HTTP URL security, '*' is not allowed in base URLs, but can be put in query strings.  This does not change the call response at all.
        countryTask = GetAsync<UngerboeckSDKPackage.CountriesModel>(USISDKClient, $"Countries/{astrOrgCode}?code=***");
      }
      else
      {
        countryTask = GetAsync<UngerboeckSDKPackage.CountriesModel>(USISDKClient, $"Countries/{astrOrgCode}/{astrCode}");
      }

      return countryTask.Result;
    }
    [Obsolete("Use GetCountry to get a single item or GetSearchList for a list")]
    public static UngerboeckSDKPackage.CountriesModel GetCountries(HttpClient USISDKClient, string astrOrgCode, string astrCode)
    {
      Task<UngerboeckSDKPackage.CountriesModel> countryTask =
          GetAsync<UngerboeckSDKPackage.CountriesModel>(USISDKClient, $"Countries/{astrOrgCode}/{astrCode}");
      return countryTask.Result;
    }
    public static UngerboeckSDKPackage.CustomerTermsModel GetCustomerTerm(HttpClient USISDKClient, string astrOrgCode, string astrCode)
    {
      Task<UngerboeckSDKPackage.CustomerTermsModel> customerTermTask =
          GetAsync<UngerboeckSDKPackage.CustomerTermsModel>(USISDKClient, $"CustomerTerms/{astrOrgCode}/{astrCode}");
      return customerTermTask.Result;
    }
    [Obsolete("Use GetCustomerTerm to get a single item or GetSearchList for a list")]
    public static UngerboeckSDKPackage.CustomerTermsModel GetCustomerTerms(HttpClient USISDKClient, string astrOrgCode, string astrCode)
    {
      Task<UngerboeckSDKPackage.CustomerTermsModel> customerTermTask =
          GetAsync<UngerboeckSDKPackage.CustomerTermsModel>(USISDKClient, $"CustomerTerms/{astrOrgCode}/{astrCode}");
      return customerTermTask.Result;
    }
    public static UngerboeckSDKPackage.CustomFieldSetsModel GetCustomFieldSet(HttpClient USISDKClient, string astrOrgCode, string astrClass, string astrCode)
    {
      Task<UngerboeckSDKPackage.CustomFieldSetsModel> customFieldSetTask =
          GetAsync<UngerboeckSDKPackage.CustomFieldSetsModel>(USISDKClient, $"CustomFieldSets/{astrOrgCode}/{astrClass}/{astrCode}");
      return customFieldSetTask.Result;
    }
    [Obsolete("Use GetCustomFieldSet to get a single item or GetSearchList for a list")]
    public static UngerboeckSDKPackage.CustomFieldSetsModel GetCustomFieldSets(HttpClient USISDKClient, string astrOrgCode, string astrClass, string astrCode)
    {
      Task<UngerboeckSDKPackage.CustomFieldSetsModel> customFieldSetTask =
          GetAsync<UngerboeckSDKPackage.CustomFieldSetsModel>(USISDKClient, $"CustomFieldSets/{astrOrgCode}/{astrClass}/{astrCode}");
      return customFieldSetTask.Result;
    }
    public static UngerboeckSDKPackage.CustomFieldValidationTablesModel GetCustomFieldValidationTable(HttpClient USISDKClient, string astrOrgCode, int aintID)
    {
      Task<UngerboeckSDKPackage.CustomFieldValidationTablesModel> customFieldValidationTableTask =
          GetAsync<UngerboeckSDKPackage.CustomFieldValidationTablesModel>(USISDKClient, $"CustomFieldValidationTables/{astrOrgCode}/{aintID}");
      return customFieldValidationTableTask.Result;
    }
    [Obsolete("Use GetCustomFieldValidationTable to get a single item or GetSearchList for a list")]
    public static UngerboeckSDKPackage.CustomFieldValidationTablesModel GetCustomFieldValidationTables(HttpClient USISDKClient, string astrOrgCode, int aintID)
    {
      Task<UngerboeckSDKPackage.CustomFieldValidationTablesModel> customFieldValidationTableTask =
          GetAsync<UngerboeckSDKPackage.CustomFieldValidationTablesModel>(USISDKClient, $"CustomFieldValidationTables/{astrOrgCode}/{aintID}");
      return customFieldValidationTableTask.Result;
    }
    public static UngerboeckSDKPackage.CyclesModel GetCycle(HttpClient USISDKClient, string astrOrgCode, string astrCode)
    {
      Task<UngerboeckSDKPackage.CyclesModel> cycleTask =
          GetAsync<UngerboeckSDKPackage.CyclesModel>(USISDKClient, $"Cycles/{astrOrgCode}/{astrCode}");
      return cycleTask.Result;
    }
    [Obsolete("Use GetCycle to get a single item or GetSearchList for a list")]
    public static UngerboeckSDKPackage.CyclesModel GetCycles(HttpClient USISDKClient, string astrOrgCode, string astrCode)
    {
      Task<UngerboeckSDKPackage.CyclesModel> cycleTask =
          GetAsync<UngerboeckSDKPackage.CyclesModel>(USISDKClient, $"Cycles/{astrOrgCode}/{astrCode}");
      return cycleTask.Result;
    }

    public static UngerboeckSDKPackage.DepartmentsModel GetDepartment(HttpClient USISDKClient, string astrOrgCode, string astrCode)
    {
      Task<UngerboeckSDKPackage.DepartmentsModel> departmentTask =
          GetAsync<UngerboeckSDKPackage.DepartmentsModel>(USISDKClient, $"Departments/{astrOrgCode}/{astrCode}");
      return departmentTask.Result;
    }
    [Obsolete("Use GetDepartment to get a single item or GetSearchList for a list")]
    public static UngerboeckSDKPackage.DepartmentsModel GetDepartments(HttpClient USISDKClient, string astrOrgCode, string astrCode)
    {
      Task<UngerboeckSDKPackage.DepartmentsModel> departmentTask =
          GetAsync<UngerboeckSDKPackage.DepartmentsModel>(USISDKClient, $"Departments/{astrOrgCode}/{astrCode}");
      return departmentTask.Result;
    }

    public static UngerboeckSDKPackage.DistributionsModel GetDistribution(HttpClient USISDKClient, string astrOrgCode, string astrBulletinApplication, int aintMeeting, int aintBulletin, int aintDistributionEntrySeqNbr)
    {
      Task<UngerboeckSDKPackage.DistributionsModel> distributionTask =
          GetAsync<UngerboeckSDKPackage.DistributionsModel>(USISDKClient, $"Distributions/{astrOrgCode}/{astrBulletinApplication}/{aintMeeting}/{aintBulletin}/{aintDistributionEntrySeqNbr}");
      return distributionTask.Result;
    }
    [Obsolete("Use GetDistribution to get a single item or GetSearchList for a list")]
    public static UngerboeckSDKPackage.DistributionsModel GetDistributions(HttpClient USISDKClient, string astrOrgCode, string astrBulletinApplication, int aintMeeting, int aintBulletin, int aintDistributionEntrySeqNbr)
    {
      Task<UngerboeckSDKPackage.DistributionsModel> distributionTask =
          GetAsync<UngerboeckSDKPackage.DistributionsModel>(USISDKClient, $"Distributions/{astrOrgCode}/{astrBulletinApplication}/{aintMeeting}/{aintBulletin}/{aintDistributionEntrySeqNbr}");
      return distributionTask.Result;
    }
    public static UngerboeckSDKPackage.DocumentClassesModel GetDocumentClass(HttpClient USISDKClient, string astrOrgCode, string astrClass)
    {
      Task<UngerboeckSDKPackage.DocumentClassesModel> documentClassTask =
          GetAsync<UngerboeckSDKPackage.DocumentClassesModel>(USISDKClient, $"DocumentClasses/{astrOrgCode}/{astrClass}");
      return documentClassTask.Result;
    }
    [Obsolete("Use GetDocumentClass to get a single item or GetSearchList for a list")]
    public static UngerboeckSDKPackage.DocumentClassesModel GetDocumentClasses(HttpClient USISDKClient, string astrOrgCode, string astrClass)
    {
      Task<UngerboeckSDKPackage.DocumentClassesModel> documentClassTask =
          GetAsync<UngerboeckSDKPackage.DocumentClassesModel>(USISDKClient, $"DocumentClasses/{astrOrgCode}/{astrClass}");
      return documentClassTask.Result;
    }
    [Obsolete("Use GetExhibitor to get a single item or GetSearchList for a list")]
    public static UngerboeckSDKPackage.ExhibitorsModel GetExhibitors(HttpClient USISDKClient, string astrOrgCode, int aintExhibitorID)
    {
      Task<UngerboeckSDKPackage.ExhibitorsModel> exhibitorTask =
          GetAsync<UngerboeckSDKPackage.ExhibitorsModel>(USISDKClient, $"Exhibitors/{astrOrgCode}/{aintExhibitorID}");
      return exhibitorTask.Result;
    }
    public static UngerboeckSDKPackage.GlAccountsModel GetGLAccount(HttpClient USISDKClient, string astrOrgCode, string astrGLAccount, string subAccount)
    {
      Task<UngerboeckSDKPackage.GlAccountsModel> gLAccountTask =
          GetAsync<UngerboeckSDKPackage.GlAccountsModel>(USISDKClient, $"GLAccounts/{astrOrgCode}/{astrGLAccount}/{subAccount}");
      return gLAccountTask.Result;
    }
    [Obsolete("Use GetGLAccount to get a single item or GetSearchList for a list")]
    public static UngerboeckSDKPackage.GlAccountsModel GetGLAccounts(HttpClient USISDKClient, string astrOrgCode, string astrGLAccount, string subAccount)
    {
      Task<UngerboeckSDKPackage.GlAccountsModel> gLAccountTask =
          GetAsync<UngerboeckSDKPackage.GlAccountsModel>(USISDKClient, $"GLAccounts/{astrOrgCode}/{astrGLAccount}/{subAccount}");
      return gLAccountTask.Result;
    }
    public static UngerboeckSDKPackage.InvoicesModel GetInvoice(HttpClient USISDKClient, string astrOrgCode, int aintInvoiceNumber, string astrSource)
    {
      Task<UngerboeckSDKPackage.InvoicesModel> invoicesTask =
          GetAsync<UngerboeckSDKPackage.InvoicesModel>(USISDKClient, $"Invoices/{astrOrgCode}/{aintInvoiceNumber}/{astrSource}");
      return invoicesTask.Result;
    }

    public static UngerboeckSDKPackage.InvoiceDetailsModel GetInvoiceDetail(HttpClient USISDKClient, string astrOrgCode, int aintInvoiceNumber, int aintOrderNumber, int aintOrderLine)
    {
      Task<UngerboeckSDKPackage.InvoiceDetailsModel> invoiceDetailTask =
          GetAsync<UngerboeckSDKPackage.InvoiceDetailsModel>(USISDKClient, $"InvoiceDetails/{astrOrgCode}/{aintInvoiceNumber}/{aintOrderNumber}/{aintOrderLine}");
      return invoiceDetailTask.Result;
    }

    public static UngerboeckSDKPackage.InvoiceDetailTaxesModel GetInvoiceDetailTax(HttpClient USISDKClient, string astrOrgCode, int aintInvoiceNumber, int aintOrderNumber, int aintOrderLine, int aintSequenceNbr)
    {
      Task<UngerboeckSDKPackage.InvoiceDetailTaxesModel> invoiceDetailTaxesTask =
          GetAsync<UngerboeckSDKPackage.InvoiceDetailTaxesModel>(USISDKClient, $"InvoiceDetailTaxes/{astrOrgCode}/{aintInvoiceNumber}/{aintOrderNumber}/{aintOrderLine}/{aintSequenceNbr}");
      return invoiceDetailTaxesTask.Result;
    }

    [Obsolete("Use GetInvoice to get a single item or GetSearchList for a list")]
    public static UngerboeckSDKPackage.InvoicesModel GetInvoices(HttpClient USISDKClient, string astrOrgCode, int aintInvoiceNumber, string astrSource)
    {
      Task<UngerboeckSDKPackage.InvoicesModel> invoicesTask =
          GetAsync<UngerboeckSDKPackage.InvoicesModel>(USISDKClient, $"Invoices/{astrOrgCode}/{aintInvoiceNumber}/{astrSource}");
      return invoicesTask.Result;
    }

    public static UngerboeckSDKPackage.InvoicesModel UpdateInvoice(HttpClient USISDKClient, UngerboeckSDKPackage.InvoicesModel invoice)
    {
      Task<UngerboeckSDKPackage.InvoicesModel> invoiceTask = PutAsync(USISDKClient, $"Invoices/{invoice.OrganizationCode}/{invoice.InvoiceNumber}/{invoice.Source}", invoice);
      return invoiceTask.Result;
    }

    public static UngerboeckSDKPackage.JournalEntriesModel GetJournalEntry(HttpClient USISDKClient, string astrOrgCode, int aintYear, int aintPeriod, string astrSource, string astrEntryNumber)
    {
      Task<UngerboeckSDKPackage.JournalEntriesModel> journalEntriesTask =
          GetAsync<UngerboeckSDKPackage.JournalEntriesModel>(USISDKClient, $"JournalEntries/{astrOrgCode}/{aintYear}/{aintPeriod}/{astrSource}/{astrEntryNumber}");
      return journalEntriesTask.Result;
    }
    [Obsolete("Use GetJournalEntry to get a single item or GetSearchList for a list")]
    public static UngerboeckSDKPackage.JournalEntriesModel GetJournalEntries(HttpClient USISDKClient, string astrOrgCode, int aintYear, int aintPeriod, string astrSource, string astrEntryNumber)
    {
      Task<UngerboeckSDKPackage.JournalEntriesModel> journalEntriesTask =
          GetAsync<UngerboeckSDKPackage.JournalEntriesModel>(USISDKClient, $"JournalEntries/{astrOrgCode}/{aintYear}/{aintPeriod}/{astrSource}/{astrEntryNumber}");
      return journalEntriesTask.Result;
    }
    public static UngerboeckSDKPackage.JournalEntryDetailsModel GetJournalEntryDetail(HttpClient USISDKClient, string astrOrgCode, int aintYear, int aintPeriod, string astrSource, string astrEntryNumber, int Line)
    {
      Task<UngerboeckSDKPackage.JournalEntryDetailsModel> journalEntryDetailTask =
          GetAsync<UngerboeckSDKPackage.JournalEntryDetailsModel>(USISDKClient, $"JournalEntryDetails/{astrOrgCode}/{aintYear}/{aintPeriod}/{astrSource}/{astrEntryNumber}/{Line}");
      return journalEntryDetailTask.Result;
    }
    [Obsolete("Use GetJournalEntryDetail to get a single item or GetSearchList for a list")]
    public static UngerboeckSDKPackage.JournalEntryDetailsModel GetJournalEntryDetails(HttpClient USISDKClient, string astrOrgCode, int aintYear, int aintPeriod, string astrSource, string astrEntryNumber, int Line)
    {
      Task<UngerboeckSDKPackage.JournalEntryDetailsModel> journalEntryDetailTask =
          GetAsync<UngerboeckSDKPackage.JournalEntryDetailsModel>(USISDKClient, $"JournalEntryDetails/{astrOrgCode}/{aintYear}/{aintPeriod}/{astrSource}/{astrEntryNumber}/{Line}");
      return journalEntryDetailTask.Result;
    }


    public static UngerboeckSDKPackage.JournalEntryDetailsModel AddJournalEntryDetail(HttpClient USISDKClient, UngerboeckSDKPackage.JournalEntryDetailsModel objModel)
    {
      Task<UngerboeckSDKPackage.JournalEntryDetailsModel> journalEntryDetailTask = PostAsync(USISDKClient, "JournalEntryDetails", objModel);
      return journalEntryDetailTask.Result;
    }

    public static UngerboeckSDKPackage.JournalEntryDetailsModel UpdateJouralEntryDetail(HttpClient USISDKClient, UngerboeckSDKPackage.JournalEntryDetailsModel journalEntryDetail)
    {
      Task<UngerboeckSDKPackage.JournalEntryDetailsModel> journalEntryDetailsTask = PutAsync(USISDKClient,
          $"JournalEntryDetails/{journalEntryDetail.Organization}/{journalEntryDetail.Year}/{journalEntryDetail.Period}/{journalEntryDetail.Source}/{journalEntryDetail.EntryNumber}/{journalEntryDetail.Line}", journalEntryDetail);
      return journalEntryDetailsTask.Result;
    }

    public static UngerboeckSDKPackage.JournalEntriesModel AddJournalEntry(HttpClient USISDKClient, UngerboeckSDKPackage.JournalEntriesModel objModel)
    {
      Task<UngerboeckSDKPackage.JournalEntriesModel> journalEntryTask = PostAsync(USISDKClient, "JournalEntries", objModel);
      return journalEntryTask.Result;
    }

    public static UngerboeckSDKPackage.JournalEntriesModel UpdateJouralEntry(HttpClient USISDKClient, UngerboeckSDKPackage.JournalEntriesModel journalEntry)
    {
      Task<UngerboeckSDKPackage.JournalEntriesModel> journalEntryTask = PutAsync(USISDKClient,
          $"JournalEntries/{journalEntry.Organization}/{journalEntry.Year}/{journalEntry.Period}/{journalEntry.Source}/{journalEntry.EntryNumber}", journalEntry);
      return journalEntryTask.Result;
    }
    public static UngerboeckSDKPackage.MailingListsModel GetMailingList(HttpClient USISDKClient, string astrOrgCode, int aintID)
    {
      Task<UngerboeckSDKPackage.MailingListsModel> mailingListTask =
          GetAsync<UngerboeckSDKPackage.MailingListsModel>(USISDKClient, $"MailingLists/{astrOrgCode}/{aintID}");
      return mailingListTask.Result;
    }
    [Obsolete("Use GetMailingList to get a single item or GetSearchList for a list")]
    public static UngerboeckSDKPackage.MailingListsModel GetMailingLists(HttpClient USISDKClient, string astrOrgCode, int aintID)
    {
      Task<UngerboeckSDKPackage.MailingListsModel> mailingListTask =
          GetAsync<UngerboeckSDKPackage.MailingListsModel>(USISDKClient, $"MailingLists/{astrOrgCode}/{aintID}");
      return mailingListTask.Result;
    }
    public static UngerboeckSDKPackage.MarketSegmentsModel GetMarketSegment(HttpClient USISDKClient, string astrOrgCode, string astrMajor, string astrMinor)
    {
      Task<UngerboeckSDKPackage.MarketSegmentsModel> marketSegmentTask =
          GetAsync<UngerboeckSDKPackage.MarketSegmentsModel>(USISDKClient, $"MarketSegments/{astrOrgCode}/{astrMajor}/{astrMinor}");
      return marketSegmentTask.Result;
    }
    [Obsolete("Use GetMarketSegment to get a single item or GetSearchList for a list")]
    public static UngerboeckSDKPackage.MarketSegmentsModel GetMarketSegments(HttpClient USISDKClient, string astrOrgCode, string astrMajor, string astrMinor)
    {
      Task<UngerboeckSDKPackage.MarketSegmentsModel> marketSegmentTask =
          GetAsync<UngerboeckSDKPackage.MarketSegmentsModel>(USISDKClient, $"MarketSegments/{astrOrgCode}/{astrMajor}/{astrMinor}");
      return marketSegmentTask.Result;
    }
    public static UngerboeckSDKPackage.MeetingFlowPatternModel GetMeetingFlowPattern(HttpClient USISDKClient, string astrOrgCode, string astrFlowApplicationCode, int aintMeetingTourSequenceNbr, int aintFlowSequenceNumber)
    {
      Task<UngerboeckSDKPackage.MeetingFlowPatternModel> meetingFlowPatternTask =
          GetAsync<UngerboeckSDKPackage.MeetingFlowPatternModel>(USISDKClient, $"MeetingFlowPattern/{astrOrgCode}/{astrFlowApplicationCode}/{aintMeetingTourSequenceNbr}/{aintFlowSequenceNumber}");
      return meetingFlowPatternTask.Result;
    }
    public static UngerboeckSDKPackage.MeetingNotesModel GetMeetingNote(HttpClient USISDKClient, string astrOrgCode, string astrBulletinApplication, int aintMeeting, int aintBulletinSeqNbr, int aintSequenceNbr)
    {
      Task<UngerboeckSDKPackage.MeetingNotesModel> meetingNotesTask =
          GetAsync<UngerboeckSDKPackage.MeetingNotesModel>(USISDKClient, $"MeetingNotes/{astrOrgCode}/{astrBulletinApplication}/{aintMeeting}/{aintBulletinSeqNbr}/{aintSequenceNbr}");
      return meetingNotesTask.Result;
    }
    [Obsolete("Use GetMeetingNote to get a single item or GetSearchList for a list")]
    public static UngerboeckSDKPackage.MeetingNotesModel GetMeetingNotes(HttpClient USISDKClient, string astrOrgCode, string astrBulletinApplication, int aintMeeting, int aintBulletinSeqNbr, int aintSequenceNbr)
    {
      Task<UngerboeckSDKPackage.MeetingNotesModel> meetingNotesTask =
          GetAsync<UngerboeckSDKPackage.MeetingNotesModel>(USISDKClient, $"MeetingNotes/{astrOrgCode}/{astrBulletinApplication}/{aintMeeting}/{aintBulletinSeqNbr}/{aintSequenceNbr}");
      return meetingNotesTask.Result;
    }
    public static UngerboeckSDKPackage.MeetingsModel GetMeeting(HttpClient USISDKClient, string astrOrgCode, int aintMeetingSequence)
    {
      Task<UngerboeckSDKPackage.MeetingsModel> meetingsTask =
          GetAsync<UngerboeckSDKPackage.MeetingsModel>(USISDKClient, $"Meetings/{astrOrgCode}/{aintMeetingSequence}");
      return meetingsTask.Result;
    }
    [Obsolete("Use GetMeeting to get a single item or GetSearchList for a list")]
    public static UngerboeckSDKPackage.MeetingsModel GetMeetings(HttpClient USISDKClient, string astrOrgCode, int aintMeetingSequence)
    {
      Task<UngerboeckSDKPackage.MeetingsModel> meetingsTask =
          GetAsync<UngerboeckSDKPackage.MeetingsModel>(USISDKClient, $"Meetings/{astrOrgCode}/{aintMeetingSequence}");
      return meetingsTask.Result;
    }
    public static UngerboeckSDKPackage.OpportunityStatusesModel GetOpportunityStatus(HttpClient USISDKClient, string astrOrgCode, string astrCode, string astrDesignation)
    {
      Task<UngerboeckSDKPackage.OpportunityStatusesModel> opportunityStatusTask =
          GetAsync<UngerboeckSDKPackage.OpportunityStatusesModel>(USISDKClient, $"OpportunityStatuses/{astrOrgCode}/{astrCode}/{astrDesignation}");
      return opportunityStatusTask.Result;
    }
    [Obsolete("Use GetOpportunityStatus to get a single item or GetSearchList for a list")]
    public static UngerboeckSDKPackage.OpportunityStatusesModel GetOpportunityStatuses(HttpClient USISDKClient, string astrOrgCode, string astrCode, string astrDesignation)
    {
      Task<UngerboeckSDKPackage.OpportunityStatusesModel> opportunityStatusTask =
          GetAsync<UngerboeckSDKPackage.OpportunityStatusesModel>(USISDKClient, $"OpportunityStatuses/{astrOrgCode}/{astrCode}/{astrDesignation}");
      return opportunityStatusTask.Result;
    }
    public static UngerboeckSDKPackage.OrderRegistrantsModel GetOrderRegistrant(HttpClient USISDKClient, string astrOrgCode, int aintRegistrantSequenceNbr)
    {
      Task<UngerboeckSDKPackage.OrderRegistrantsModel> orderRegistrantTask =
          GetAsync<UngerboeckSDKPackage.OrderRegistrantsModel>(USISDKClient, $"OrderRegistrants/{astrOrgCode}/{aintRegistrantSequenceNbr}");
      return orderRegistrantTask.Result;
    }
    [Obsolete("Use GetOrderRegistrant to get a single item or GetSearchList for a list")]
    public static UngerboeckSDKPackage.OrderRegistrantsModel GetOrderRegistrants(HttpClient USISDKClient, string astrOrgCode, int aintRegistrantSequenceNbr)
    {
      Task<UngerboeckSDKPackage.OrderRegistrantsModel> orderRegistrantTask =
          GetAsync<UngerboeckSDKPackage.OrderRegistrantsModel>(USISDKClient, $"OrderRegistrants/{astrOrgCode}/{aintRegistrantSequenceNbr}");
      return orderRegistrantTask.Result;
    }
    public static UngerboeckSDKPackage.OrderRegistrantsModel UpdateOrderRegistrant(HttpClient USISDKClient, UngerboeckSDKPackage.OrderRegistrantsModel orderRegistrant)
    {
      Task<UngerboeckSDKPackage.OrderRegistrantsModel> orderRegistrantTask = PutAsync(USISDKClient, $"OrderRegistrants/{orderRegistrant.OrganizationCode}/{orderRegistrant.RegistrantSequenceNbr}", orderRegistrant);
      return orderRegistrantTask.Result;
    }
    public static async Task AwaitSetRegistrantApproval(HttpClient USISDKClient, UngerboeckSDKPackage.RegistrationApprovalsModel orderRegistrantApproval)
    {
      HttpResponseMessage response = await USISDKClient.PutAsJsonAsync(
         $"{USISDKClient.BaseAddress}/api/v1/OrderRegistrants/{orderRegistrantApproval.OrganizationCode}/{orderRegistrantApproval.RegistrantSequenceNbr}/SetRegistrantApproval", orderRegistrantApproval).ConfigureAwait(false);
      APIUtil.SuccessResponse(response);
    }

    public static HttpResponseMessage SetRegistrantApproval(HttpClient USISDKClient, UngerboeckSDKPackage.RegistrationApprovalsModel orderRegistrantApproval)
    {
      Task<HttpResponseMessage> response = USISDKClient.PutAsJsonAsync($"{USISDKClient.BaseAddress}/api/v1/OrderRegistrants/{orderRegistrantApproval.OrganizationCode}/{orderRegistrantApproval.RegistrantSequenceNbr}/SetRegistrantApproval", orderRegistrantApproval);

      return response.Result;
    }

    public static async Task AwaitCheckIn(HttpClient USISDKClient, string orgCode, int registrantSequenceNbr, int eventId)
    {
      HttpResponseMessage response = await USISDKClient.PutAsync($"{USISDKClient.BaseAddress}/api/v1/OrderRegistrants/{orgCode}/{registrantSequenceNbr}/{eventId}/CheckIn", null).ConfigureAwait(false);
      APIUtil.SuccessResponse(response);
    }

    public static async Task AwaitClearCheckIn(HttpClient USISDKClient, string orgCode, int registrantSequenceNbr, int eventId)
    {
      HttpResponseMessage response = await USISDKClient.PutAsync($"{USISDKClient.BaseAddress}/api/v1/OrderRegistrants/{orgCode}/{registrantSequenceNbr}/{eventId}/ClearCheckIn", null).ConfigureAwait(false);
      APIUtil.SuccessResponse(response);
    }

    public static UngerboeckSDKPackage.OrganizationParametersModel GetOrganizationParameter(HttpClient USISDKClient, string astrOrgCode, string astrApplicationCode, string astrParameterCode)
    {
      Task<UngerboeckSDKPackage.OrganizationParametersModel> orderRegistrantTask =
          GetAsync<UngerboeckSDKPackage.OrganizationParametersModel>(USISDKClient, $"OrganizationParameters/{astrOrgCode}/{astrApplicationCode}/{astrParameterCode}");
      return orderRegistrantTask.Result;
    }
    [Obsolete("Use GetOrganizationParameter to get a single item or GetSearchList for a list")]
    public static UngerboeckSDKPackage.OrganizationParametersModel GetOrganizationParameters(HttpClient USISDKClient, string astrOrgCode, string astrApplicationCode, string astrParameterCode)
    {
      Task<UngerboeckSDKPackage.OrganizationParametersModel> orderRegistrantTask =
          GetAsync<UngerboeckSDKPackage.OrganizationParametersModel>(USISDKClient, $"OrganizationParameters/{astrOrgCode}/{astrApplicationCode}/{astrParameterCode}");
      return orderRegistrantTask.Result;
    }
    public static UngerboeckSDKPackage.PaymentPlanDetailsModel GetPaymentPlanDetail(HttpClient USISDKClient, string astrOrgCode, int aintPayPlanID, int aintPayNumber, int aintSequence)
    {
      Task<UngerboeckSDKPackage.PaymentPlanDetailsModel> paymentPlanDetailTask =
          GetAsync<UngerboeckSDKPackage.PaymentPlanDetailsModel>(USISDKClient, $"PaymentPlanDetails/{astrOrgCode}/{aintPayPlanID}/{aintPayNumber}/{aintSequence}");
      return paymentPlanDetailTask.Result;
    }
    [Obsolete("Use GetPaymentPlanDetail to get a single item or GetSearchList for a list")]
    public static UngerboeckSDKPackage.PaymentPlanDetailsModel GetPaymentPlanDetails(HttpClient USISDKClient, string astrOrgCode, int aintPayPlanID, int aintPayNumber, int aintSequence)
    {
      Task<UngerboeckSDKPackage.PaymentPlanDetailsModel> paymentPlanDetailTask =
          GetAsync<UngerboeckSDKPackage.PaymentPlanDetailsModel>(USISDKClient, $"PaymentPlanDetails/{astrOrgCode}/{aintPayPlanID}/{aintPayNumber}/{aintSequence}");
      return paymentPlanDetailTask.Result;
    }
    public static UngerboeckSDKPackage.PaymentPlanHeadersModel GetPaymentPlanHeader(HttpClient USISDKClient, string astrOrgCode, int aintPayPlanID, int aintPayNumber)
    {
      Task<UngerboeckSDKPackage.PaymentPlanHeadersModel> paymentPlanHeaderTask =
          GetAsync<UngerboeckSDKPackage.PaymentPlanHeadersModel>(USISDKClient, $"PaymentPlanHeaders/{astrOrgCode}/{aintPayPlanID}/{aintPayNumber}");
      return paymentPlanHeaderTask.Result;
    }
    [Obsolete("Use GetPaymentPlanHeader to get a single item or GetSearchList for a list")]
    public static UngerboeckSDKPackage.PaymentPlanHeadersModel GetPaymentPlanHeaders(HttpClient USISDKClient, string astrOrgCode, int aintPayPlanID, int aintPayNumber)
    {
      Task<UngerboeckSDKPackage.PaymentPlanHeadersModel> paymentPlanHeaderTask =
          GetAsync<UngerboeckSDKPackage.PaymentPlanHeadersModel>(USISDKClient, $"PaymentPlanHeaders/{astrOrgCode}/{aintPayPlanID}/{aintPayNumber}");
      return paymentPlanHeaderTask.Result;
    }
    public static UngerboeckSDKPackage.PaymentPlansModel GetPaymentPlan(HttpClient USISDKClient, string astrOrgCode, int aintPaymentPlanID)
    {
      Task<UngerboeckSDKPackage.PaymentPlansModel> paymentPlanTask =
          GetAsync<UngerboeckSDKPackage.PaymentPlansModel>(USISDKClient, $"PaymentPlans/{astrOrgCode}/{aintPaymentPlanID}");
      return paymentPlanTask.Result;
    }
    [Obsolete("Use GetPaymentPlan to get a single item or GetSearchList for a list")]
    public static UngerboeckSDKPackage.PaymentPlansModel GetPaymentPlans(HttpClient USISDKClient, string astrOrgCode, int aintPaymentPlanID)
    {
      Task<UngerboeckSDKPackage.PaymentPlansModel> paymentPlanTask =
          GetAsync<UngerboeckSDKPackage.PaymentPlansModel>(USISDKClient, $"PaymentPlans/{astrOrgCode}/{aintPaymentPlanID}");
      return paymentPlanTask.Result;
    }
    public static UngerboeckSDKPackage.PriceListModel GetPriceList(HttpClient USISDKClient, string astrOrgCode, string astrCode)
    {
      Task<UngerboeckSDKPackage.PriceListModel> priceListTask =
          GetAsync<UngerboeckSDKPackage.PriceListModel>(USISDKClient, $"PriceList/{astrOrgCode}/{astrCode}");
      return priceListTask.Result;
    }
    public static UngerboeckSDKPackage.PriceListItemsModel GetPriceListItem(HttpClient USISDKClient, string astrOrgCode, string astrPriceList, int aintSequence)
    {
      Task<UngerboeckSDKPackage.PriceListItemsModel> priceListItemTask =
          GetAsync<UngerboeckSDKPackage.PriceListItemsModel>(USISDKClient, $"PriceListItems/{astrOrgCode}/{astrPriceList}/{aintSequence}");
      return priceListItemTask.Result;
    }

    public static UngerboeckSDKPackage.InventoryStatsModel GetInventoryStat(HttpClient USISDKClient, string astrOrgCode, string astrItem, string astrSpace, string astrLotSerialNumber)
    {
      Task<UngerboeckSDKPackage.InventoryStatsModel> inventoryStats =
          GetAsync<UngerboeckSDKPackage.InventoryStatsModel>(USISDKClient, $"InventoryStats/{astrOrgCode}/{astrItem}/{astrSpace}/{astrLotSerialNumber}");
      return inventoryStats.Result;
    }

    public static UngerboeckSDKPackage.InventoryItemsModel GetInventoryItem(HttpClient USISDKClient, string astrOrgCode, string astrCode)
    {
      Task<UngerboeckSDKPackage.InventoryItemsModel> inventoryItems =
          GetAsync<UngerboeckSDKPackage.InventoryItemsModel>(USISDKClient, $"InventoryItems/{astrOrgCode}/{astrCode}");
      return inventoryItems.Result;
    }

    public static UngerboeckSDKPackage.PendingPaymentsModel GetPendingPayment(HttpClient USISDKClient, string astrOrgCode, int aintPendingPaymentID)
    {
      Task<UngerboeckSDKPackage.PendingPaymentsModel> pendingPayments =
          GetAsync<UngerboeckSDKPackage.PendingPaymentsModel>(USISDKClient, $"PendingPayments/{astrOrgCode}/{aintPendingPaymentID}");
      return pendingPayments.Result;
    }

    

    public static UngerboeckSDKPackage.CurrencyCodesModel GetCurrencyCode(HttpClient USISDKClient, string astrCode)
    {
      Task<UngerboeckSDKPackage.CurrencyCodesModel> currencyCode;
      if (astrCode == "***")
      {
        //Due to HTTP URL security, '*' is not allowed in base URLs, but can be put in query strings.  This does not change the call response at all.
        currencyCode = GetAsync<UngerboeckSDKPackage.CurrencyCodesModel>(USISDKClient, $"CurrencyCodes?code=***");
      }
      else
      {
        currencyCode = GetAsync<UngerboeckSDKPackage.CurrencyCodesModel>(USISDKClient, $"CurrencyCodes/{astrCode}");
      }
      return currencyCode.Result;
    }

    public static UngerboeckSDKPackage.CurrencyRatesModel GetCurrencyRate(HttpClient USISDKClient, string astrCode, int aintSequenceNumber)
    {
      Task<UngerboeckSDKPackage.CurrencyRatesModel> currencyRate;
      if (astrCode == "***")
      {
        //Due to HTTP URL security, '*' is not allowed in base URLs, but can be put in query strings.  This does not change the call response at all.
        currencyRate = GetAsync<UngerboeckSDKPackage.CurrencyRatesModel>(USISDKClient, $"CurrencyRates?SequenceNumber={aintSequenceNumber}&CurrencyCode=***");
      }
      else
      {
        currencyRate = GetAsync<UngerboeckSDKPackage.CurrencyRatesModel>(USISDKClient, $"CurrencyRates/{astrCode}/{aintSequenceNumber}");
      }
      return currencyRate.Result;
    }

    public static CurrencyRatesModel AddCurrencyRate(HttpClient USISDKClient, CurrencyRatesModel currencyRate)
    {
      Task<CurrencyRatesModel> currencyRatesTask = PostAsync(USISDKClient, "CurrencyRates", currencyRate);
      return currencyRatesTask.Result;
    }

    public static CurrencyRatesModel UpdateCurrencyRate(HttpClient USISDKClient, string astrCode, int aintSequenceNumber, CurrencyRatesModel currencyRate)
    {
      Task<CurrencyRatesModel> ratesTask;
      if (astrCode == "***")
      {
        //Due to HTTP URL security, '*' is not allowed in base URLs, but can be put in query strings.  This does not change the call response at all.
        ratesTask = PutAsync(USISDKClient, $"CurrencyRates?SequenceNumber={aintSequenceNumber}&CurrencyCode=***", currencyRate);
      }
      else
      {
        ratesTask = PutAsync(USISDKClient, $"CurrencyRates/{astrCode}/{aintSequenceNumber}", currencyRate);
      }
      return ratesTask.Result;
    }

    public static UngerboeckSDKPackage.InventorySuppliersModel GetPriceListItem(HttpClient USISDKClient, string astrOrgCode, string astrInventoryItem, string astrSupplier)
    {
      Task<UngerboeckSDKPackage.InventorySuppliersModel> inventorySuppliers =
          GetAsync<UngerboeckSDKPackage.InventorySuppliersModel>(USISDKClient, $"InventorySuppliers/{astrOrgCode}/{astrInventoryItem}/{astrSupplier}");
      return inventorySuppliers.Result;
    }


    public static UngerboeckSDKPackage.PurchaseOrderItemsModel GetPurchaseOrderItem(HttpClient USISDKClient, string astrOrgCode, int aintNumber, int aintItemSequence)
    {
      Task<UngerboeckSDKPackage.PurchaseOrderItemsModel> purchaseOrderItemTask =
          GetAsync<UngerboeckSDKPackage.PurchaseOrderItemsModel>(USISDKClient, $"PurchaseOrderItems/{astrOrgCode}/{aintNumber}/{aintItemSequence}");
      return purchaseOrderItemTask.Result;
    }
    [Obsolete("Use GetPurchaseOrderItem to get a single item or GetSearchList for a list")]
    public static UngerboeckSDKPackage.PurchaseOrderItemsModel GetPurchaseOrderItems(HttpClient USISDKClient, string astrOrgCode, int aintNumber, int aintItemSequence)
    {
      Task<UngerboeckSDKPackage.PurchaseOrderItemsModel> purchaseOrderItemTask =
          GetAsync<UngerboeckSDKPackage.PurchaseOrderItemsModel>(USISDKClient, $"PurchaseOrderItems/{astrOrgCode}/{aintNumber}/{aintItemSequence}");
      return purchaseOrderItemTask.Result;
    }
    public static UngerboeckSDKPackage.PurchaseOrdersModel GetPurchaseOrder(HttpClient USISDKClient, string astrOrgCode, int aintNumber)
    {
      Task<UngerboeckSDKPackage.PurchaseOrdersModel> purchaseOrderTask =
          GetAsync<UngerboeckSDKPackage.PurchaseOrdersModel>(USISDKClient, $"PurchaseOrders/{astrOrgCode}/{aintNumber}");
      return purchaseOrderTask.Result;
    }
    [Obsolete("Use GetPurchaseOrder to get a single item or GetSearchList for a list")]
    public static UngerboeckSDKPackage.PurchaseOrdersModel GetPurchaseOrders(HttpClient USISDKClient, string astrOrgCode, int aintNumber)
    {
      Task<UngerboeckSDKPackage.PurchaseOrdersModel> purchaseOrderTask =
          GetAsync<UngerboeckSDKPackage.PurchaseOrdersModel>(USISDKClient, $"PurchaseOrders/{astrOrgCode}/{aintNumber}");
      return purchaseOrderTask.Result;
    }

    public static UngerboeckSDKPackage.PurchaseOrdersModel AddPurchaseOrder(HttpClient USISDKClient, UngerboeckSDKPackage.PurchaseOrdersModel objModel)
    {
      Task<UngerboeckSDKPackage.PurchaseOrdersModel> purchaseOrderTask = PostAsync(USISDKClient, "PurchaseOrders", objModel);
      return purchaseOrderTask.Result;
    }

    public static UngerboeckSDKPackage.PurchaseOrderItemsModel AddPurchaseOrderItem(HttpClient USISDKClient, UngerboeckSDKPackage.PurchaseOrderItemsModel objModel)
    {
      Task<UngerboeckSDKPackage.PurchaseOrderItemsModel> purchaseOrderItemTask = PostAsync(USISDKClient, "PurchaseOrderItems", objModel);
      return purchaseOrderItemTask.Result;
    }

    public static PurchaseOrdersModel UpdatePurchaseOrder(HttpClient USISDKClient, string astrOrgCode, int aintNumber, PurchaseOrdersModel purchaseOrder)
    {
      Task<PurchaseOrdersModel> purchaseOrderTask = PutAsync(USISDKClient, $"PurchaseOrders/{astrOrgCode}/{aintNumber}", purchaseOrder);
      return purchaseOrderTask.Result;
    }

    public static PurchaseOrderItemsModel UpdatePurchaseOrderItem(HttpClient USISDKClient, string astrOrgCode, int aintNumber, int aintItemSequence, PurchaseOrderItemsModel purchaseOrderItem)
    {
      Task<PurchaseOrderItemsModel> purchaseOrderItemTask = PutAsync(USISDKClient, $"PurchaseOrderItems/{astrOrgCode}/{aintNumber}/{aintItemSequence}", purchaseOrderItem);
      return purchaseOrderItemTask.Result;
    }

    public static UngerboeckSDKPackage.PreferenceSettingsModel GetPreferenceSetting(HttpClient USISDKClient, string astrOrgCode, int aintID)
    {
      Task<UngerboeckSDKPackage.PreferenceSettingsModel> preferenceSettingTask =
          GetAsync<UngerboeckSDKPackage.PreferenceSettingsModel>(USISDKClient, $"PreferenceSettings/{astrOrgCode}/{aintID}");
      return preferenceSettingTask.Result;
    }
    [Obsolete("Use GetPreferenceSetting to get a single item or GetSearchList for a list")]
    public static UngerboeckSDKPackage.PreferenceSettingsModel GetPreferenceSettings(HttpClient USISDKClient, string astrOrgCode, int aintID)
    {
      Task<UngerboeckSDKPackage.PreferenceSettingsModel> preferenceSettingTask =
          GetAsync<UngerboeckSDKPackage.PreferenceSettingsModel>(USISDKClient, $"PreferenceSettings/{astrOrgCode}/{aintID}");
      return preferenceSettingTask.Result;
    }

    public static UngerboeckSDKPackage.PreferenceSettingsModel AddPreferenceSetting(HttpClient USISDKClient, UngerboeckSDKPackage.PreferenceSettingsModel preferenceSetting)
    {
      Task<UngerboeckSDKPackage.PreferenceSettingsModel> preferenceSettingTask = PostAsync(USISDKClient, "PreferenceSettings", preferenceSetting);
      return preferenceSettingTask.Result;
    }

    public static UngerboeckSDKPackage.PreferenceSettingsModel UpdatePreferenceSetting(HttpClient USISDKClient, UngerboeckSDKPackage.PreferenceSettingsModel preferenceSetting)
    {
      Task<UngerboeckSDKPackage.PreferenceSettingsModel> preferenceSettingTask = PutAsync(USISDKClient, $"PreferenceSettings/{preferenceSetting.Organization}/{preferenceSetting.ID}", preferenceSetting);
      return preferenceSettingTask.Result;
    }

    public static async Task AwaitDeletePreferenceSetting(HttpClient USISDKClient, string orgCode, int aintID)
    {
      HttpResponseMessage response = await USISDKClient.DeleteAsync($"{USISDKClient.BaseAddress}/api/v1/PreferenceSettings/{orgCode}/{aintID}").ConfigureAwait(false);
      APIUtil.SuccessResponse(response);
    }
    public static UngerboeckSDKPackage.ProductsAndServicesModel GetProductsAndService(HttpClient USISDKClient, string astrOrgCode, string astrCode)
    {
      Task<UngerboeckSDKPackage.ProductsAndServicesModel> productServiceTask = GetAsync<UngerboeckSDKPackage.ProductsAndServicesModel>(USISDKClient, $"ProductsAndServices/{astrOrgCode}/{astrCode}");
      return productServiceTask.Result;
    }
    [Obsolete("Use GetProductsAndService to get a single item or GetSearchList for a list")]
    public static UngerboeckSDKPackage.ProductsAndServicesModel GetProductsAndServices(HttpClient USISDKClient, string astrOrgCode, string astrCode)
    {
      Task<UngerboeckSDKPackage.ProductsAndServicesModel> productServiceTask = GetAsync<UngerboeckSDKPackage.ProductsAndServicesModel>(USISDKClient, $"ProductsAndServices/{astrOrgCode}/{astrCode}");
      return productServiceTask.Result;
    }
    public static UngerboeckSDKPackage.QuotesModel GetQuote(HttpClient USISDKClient, string astrOrgCode, int aintSequence)
    {
      Task<UngerboeckSDKPackage.QuotesModel> purchaseOrderTask =
          GetAsync<UngerboeckSDKPackage.QuotesModel>(USISDKClient, $"Quotes/{astrOrgCode}/{aintSequence}");
      return purchaseOrderTask.Result;
    }
    [Obsolete("Use GetQuote to get a single item or GetSearchList for a list")]
    public static UngerboeckSDKPackage.QuotesModel GetQuotes(HttpClient USISDKClient, string astrOrgCode, int aintSequence)
    {
      Task<UngerboeckSDKPackage.QuotesModel> purchaseOrderTask =
          GetAsync<UngerboeckSDKPackage.QuotesModel>(USISDKClient, $"Quotes/{astrOrgCode}/{aintSequence}");
      return purchaseOrderTask.Result;
    }
    public static UngerboeckSDKPackage.FunctionsModel InsertAfterFunction(HttpClient USISDKClient, int targetFunctionID, UngerboeckSDKPackage.FunctionsModel function)
    {
      Task<UngerboeckSDKPackage.FunctionsModel> functionTask = PostAsync(USISDKClient, $"Functions/InsertAfter/{targetFunctionID}", function);
      return functionTask.Result;
    }

    public static UngerboeckSDKPackage.FunctionsModel InsertIndentedFunction(HttpClient USISDKClient, int parentFunctionID, UngerboeckSDKPackage.FunctionsModel function)
    {
      Task<UngerboeckSDKPackage.FunctionsModel> functionTask = PostAsync(USISDKClient, $"Functions/InsertIndented/{parentFunctionID}", function);
      return functionTask.Result;
    }

    public static UngerboeckSDKPackage.FunctionsModel GetFunction(HttpClient USISDKClient, string astrOrgCode, int aintEventID, int aintFunctionID)
    {
      Task<UngerboeckSDKPackage.FunctionsModel> functionTask = GetAsync<UngerboeckSDKPackage.FunctionsModel>(USISDKClient,
          $"Functions/{astrOrgCode}/{aintEventID}/{aintFunctionID}");
      return functionTask.Result;
    }

    public static UngerboeckSDKPackage.FunctionsModel UpdateFunction(HttpClient USISDKClient, UngerboeckSDKPackage.FunctionsModel function)
    {
      Task<UngerboeckSDKPackage.FunctionsModel> functionTask = PutAsync(USISDKClient, $"Functions/{function.OrganizationCode}/{function.EventID}/{function.FunctionID}", function);
      return functionTask.Result;
    }

    public static async Task AwaitDeleteFunction(HttpClient USISDKClient, string astrOrgCode, int aintEventID, int aintFunctionID)
    {
      HttpResponseMessage response = await USISDKClient.DeleteAsync($"{USISDKClient.BaseAddress}/api/v1/Functions/{astrOrgCode}/{aintEventID}/{aintFunctionID}").ConfigureAwait(false);
      APIUtil.SuccessResponse(response);
    }

    public static FunctionSeatingChartsModel GetFunctionSeatingChart(HttpClient USISDKClient, string astrOrgCode, int aintSequenceNumber)
    {
      Task<FunctionSeatingChartsModel> functionSeatingChartTask = GetAsync<FunctionSeatingChartsModel>(USISDKClient,
                                                                                                       $"FunctionSeatingCharts/{astrOrgCode}/{aintSequenceNumber}");
      return functionSeatingChartTask.Result;
    }

    public static FunctionSeatingChartsModel AddFunctionSeatingChart(HttpClient USISDKClient, FunctionSeatingChartsModel @functionSeatingChart)
    {
      Task<FunctionSeatingChartsModel> functionSeatingChartTask = PostAsync(USISDKClient, "FunctionSeatingCharts", @functionSeatingChart);
      return functionSeatingChartTask.Result;
    }

    public static FunctionSeatingChartsModel UpdateFunctionSeatingChart(HttpClient USISDKClient, FunctionSeatingChartsModel @functionSeatingChart)
    {
      Task<FunctionSeatingChartsModel> functionSeatingChartTask = PutAsync(USISDKClient,
                                                                           $"FunctionSeatingCharts/{@functionSeatingChart.OrgCode}/{@functionSeatingChart.SequenceNumber}",
                                                                           @functionSeatingChart);
      return functionSeatingChartTask.Result;
    }

    public static RegistrationAssignmentsModel GetRegistrationAssignment(HttpClient USISDKClient, string astrOrgCode, int aintSequenceNumber)
    {
      Task<RegistrationAssignmentsModel> registrationAssignmentTask = GetAsync<RegistrationAssignmentsModel>(USISDKClient,
                                                                                                             $"RegistrationAssignments/{astrOrgCode}/{aintSequenceNumber}");
      return registrationAssignmentTask.Result;
    }

    public static RegistrationAssignmentsModel AddRegistrationAssignment(HttpClient USISDKClient, RegistrationAssignmentsModel @registrationAssignment)
    {
      Task<RegistrationAssignmentsModel> registrationAssignmentTask = PostAsync(USISDKClient, "RegistrationAssignments", @registrationAssignment);
      return registrationAssignmentTask.Result;
    }

    public static RegistrationAssignmentsModel UpdateRegistrationAssignment(HttpClient USISDKClient, RegistrationAssignmentsModel @registrationAssignement)
    {
      Task<RegistrationAssignmentsModel> registrationAssignmentTask = PutAsync(USISDKClient,
                                                                               $"RegistrationAssignments/{@registrationAssignement.OrgCode}/{@registrationAssignement.SequenceNumber}",
                                                                               @registrationAssignement);
      return registrationAssignmentTask.Result;
    }

    public static UngerboeckSDKPackage.CommunicationsModel GetCommunication(HttpClient USISDKClient, string astrOrgCode, string astrAccountCode, int aintSequenceNumber)
    {
      Task<UngerboeckSDKPackage.CommunicationsModel> communicationTask = GetAsync<UngerboeckSDKPackage.CommunicationsModel>(USISDKClient, $"Communications/{astrOrgCode}/{astrAccountCode}/{aintSequenceNumber}");
      return communicationTask.Result;
    }

    public static UngerboeckSDKPackage.CommunicationsModel AddCommunication(HttpClient USISDKClient, UngerboeckSDKPackage.CommunicationsModel communication)
    {
      Task<UngerboeckSDKPackage.CommunicationsModel> communicationTask = PostAsync(USISDKClient, "Communications", communication);
      return communicationTask.Result;
    }

    public static UngerboeckSDKPackage.CommunicationsModel UpdateCommunication(HttpClient USISDKClient, UngerboeckSDKPackage.CommunicationsModel communication)
    {
      Task<UngerboeckSDKPackage.CommunicationsModel> communicationTask = PutAsync(USISDKClient, $"Communications/{communication.OrganizationCode}/{communication.AccountCode}/{communication.SequenceNumber}", communication);
      return communicationTask.Result;
    }

    public static async Task AwaitDeleteCommunication(HttpClient USISDKClient, string astrOrgCode, string astrAccountCode, int aintSequenceNumber)
    {
      HttpResponseMessage response = await USISDKClient.DeleteAsync(
          $"{USISDKClient.BaseAddress}/api/v1/Communications/{astrOrgCode}/{astrAccountCode}/{aintSequenceNumber}").ConfigureAwait(false);
      APIUtil.SuccessResponse(response);
    }

    public static UngerboeckSDKPackage.OrderStatusesModel GetOrderStatus(HttpClient USISDKClient, string astrOrgCode, string astrCode)
    {
      Task<UngerboeckSDKPackage.OrderStatusesModel> OrderStatus =
          GetAsync<UngerboeckSDKPackage.OrderStatusesModel>(USISDKClient, $"OrderStatuses/{astrOrgCode}/{astrCode}");
      return OrderStatus.Result;
    }

    public static UngerboeckSDKPackage.EventStatusesModel GetEventStatus(HttpClient USISDKClient, string astrOrgCode, string astrCode)
    {
      Task<UngerboeckSDKPackage.EventStatusesModel> eventStatusTask = GetAsync<UngerboeckSDKPackage.EventStatusesModel>(USISDKClient, $"EventStatuses/{astrOrgCode}/{astrCode}");
      return eventStatusTask.Result;
    }
    public static UngerboeckSDKPackage.EventsPriceListModel GetEventsPriceList(HttpClient USISDKClient, string astrOrgCode, string astrCode, int aintEventID)
    {
      Task<UngerboeckSDKPackage.EventsPriceListModel> eventPriceListTask = GetAsync<UngerboeckSDKPackage.EventsPriceListModel>(USISDKClient, $"EventsPriceList/{astrOrgCode}/{astrCode}/{aintEventID}");
      return eventPriceListTask.Result;
    }
    public static UngerboeckSDKPackage.EventJobCategoriesModel GetEventJobCategory(HttpClient USISDKClient, string astrOrgCode, string astrCode)
    {
      Task<UngerboeckSDKPackage.EventJobCategoriesModel> eventJobCategoryTask = GetAsync<UngerboeckSDKPackage.EventJobCategoriesModel>(USISDKClient, $"EventJobCategories/{astrOrgCode}/{astrCode}");
      return eventJobCategoryTask.Result;
    }
    public static UngerboeckSDKPackage.EventJobClassesModel GetEventJobClass(HttpClient USISDKClient, string astrOrgCode, string astrCode)
    {
      Task<UngerboeckSDKPackage.EventJobClassesModel> eventJobClassTask = GetAsync<UngerboeckSDKPackage.EventJobClassesModel>(USISDKClient, $"EventJobClasses/{astrOrgCode}/{astrCode}");
      return eventJobClassTask.Result;
    }
    public static UngerboeckSDKPackage.EventJobTypesModel GetEventJobType(HttpClient USISDKClient, string astrOrgCode, string astrCode)
    {
      Task<UngerboeckSDKPackage.EventJobTypesModel> eventJobTypeTask = GetAsync<UngerboeckSDKPackage.EventJobTypesModel>(USISDKClient, $"EventJobTypes/{astrOrgCode}/{astrCode}");
      return eventJobTypeTask.Result;
    }
    public static UngerboeckSDKPackage.EventProductsAndServicesModel GetEventProductService(HttpClient USISDKClient, string astrOrgCode, int aintSequenceNumber)
    {
      Task<UngerboeckSDKPackage.EventProductsAndServicesModel> eventProductAndServiceTask = GetAsync<UngerboeckSDKPackage.EventProductsAndServicesModel>(USISDKClient, $"EventProductsAndServices/{astrOrgCode}/{aintSequenceNumber}");
      return eventProductAndServiceTask.Result;
    }
    public static EventProductsAndServicesModel AddEventProductService(HttpClient USISDKClient, EventProductsAndServicesModel product)
    {
      Task<EventProductsAndServicesModel> eventProductAndServiceTask = PostAsync(USISDKClient, $"EventProductsAndServices/{product.OrganizationCode}/{product.SequenceNumber}", product);
      return eventProductAndServiceTask.Result;
    }
    public static EventProductsAndServicesModel UpdateEventProductService(HttpClient USISDKClient, EventProductsAndServicesModel product)
    {
      Task<EventProductsAndServicesModel> eventProductAndServiceTask = PutAsync(USISDKClient, $"EventProductsAndServices/{product.OrganizationCode}/{product.SequenceNumber}", product);
      return eventProductAndServiceTask.Result;
    }

    public static UngerboeckSDKPackage.EventServicesModel GetEventService(HttpClient USISDKClient, string astrOrgCode, int aintEventID, int aintSequenceNumber)
    {
      Task<UngerboeckSDKPackage.EventServicesModel> eventServiceTask = GetAsync<UngerboeckSDKPackage.EventServicesModel>(USISDKClient, $"EventServices/{astrOrgCode}/{aintEventID}/{aintSequenceNumber}");
      return eventServiceTask.Result;
    }

    public static EventServicesModel AddEventService(HttpClient USISDKClient, EventServicesModel eventService)
    {
      Task<EventServicesModel> eventServiceTask = PostAsync(USISDKClient, $"EventServices/{eventService.OrganizationCode}/{eventService.EventID}/{eventService.SequenceNumber}", eventService);
      return eventServiceTask.Result;
    }

    public static EventServicesModel UpdateEventService(HttpClient USISDKClient, EventServicesModel eventService)
    {
      Task<EventServicesModel> eventServiceTask = PutAsync(USISDKClient, $"EventServices/{eventService.OrganizationCode}/{eventService.EventID}/{eventService.SequenceNumber}", eventService);
      return eventServiceTask.Result;
    }

    public static async Task AwaitDeleteEventService(HttpClient USISDKClient, string astrOrgCode, int aintEventID, int aintSequenceNumber)
    {
      HttpResponseMessage response = await USISDKClient.DeleteAsync($"{USISDKClient.BaseAddress}/api/v1/EventServices/{astrOrgCode}/{aintEventID}/{aintSequenceNumber}").ConfigureAwait(false);
      APIUtil.SuccessResponse(response);
    }

    public static UngerboeckSDKPackage.DocumentsModel GetDocument(HttpClient USISDKClient, string astrOrgCode, string astrType, int aintSequenceNumber)
    {
      Task<UngerboeckSDKPackage.DocumentsModel> documentTask = GetAsync<UngerboeckSDKPackage.DocumentsModel>(USISDKClient,
          $"Documents/{astrOrgCode}/{astrType}/{aintSequenceNumber}");
      return documentTask.Result;
    }

    public static UngerboeckSDKPackage.DocumentsModel AddDocument(HttpClient USISDKClient, UngerboeckSDKPackage.DocumentsModel document)
    {
      Task<UngerboeckSDKPackage.DocumentsModel> documentTask = PostAsync(USISDKClient, "Documents", document);
      return documentTask.Result;
    }

    public static UngerboeckSDKPackage.DocumentsModel UpdateDocument(HttpClient USISDKClient, UngerboeckSDKPackage.DocumentsModel document)
    {
      Task<UngerboeckSDKPackage.DocumentsModel> documentTask = PutAsync(USISDKClient, $"Documents/{document.Organization}/{document.Type}/{document.SequenceNumber}", document);
      return documentTask.Result;
    }

    public static string DownloadDocument(HttpClient USISDKClient, string astrOrgCode, string astrType, int aintSequenceNumber)
    {
      Task<string> documentBytesTask =
          GetStringAsync(USISDKClient, $"Documents/{astrOrgCode}/{astrType}/{aintSequenceNumber}/Download");
      return documentBytesTask.Result;
    }
    public static async Task AwaitDeleteDocument(HttpClient USISDKClient, string orgCode, string astrType, int aintSequenceNumber)
    {
      HttpResponseMessage response = await USISDKClient.DeleteAsync($"{USISDKClient.BaseAddress}/api/v1/Documents/{orgCode}/{astrType}/{aintSequenceNumber}").ConfigureAwait(false);
      APIUtil.SuccessResponse(response);
    }
    public static UngerboeckSDKPackage.EventsModel GetEvent(HttpClient USISDKClient, string astrOrgCode, int aintEventID)
    {
      Task<UngerboeckSDKPackage.EventsModel> eventTask = GetAsync<UngerboeckSDKPackage.EventsModel>(USISDKClient,
          $"Events/{astrOrgCode}/{aintEventID}");
      return eventTask.Result;
    }

    public static UngerboeckSDKPackage.EventsModel AddEvent(HttpClient USISDKClient, UngerboeckSDKPackage.EventsModel @event)
    {
      Task<UngerboeckSDKPackage.EventsModel> eventTask = PostAsync(USISDKClient, "Events", @event);
      return eventTask.Result;
    }

    public static UngerboeckSDKPackage.EventsModel UpdateEvent(HttpClient USISDKClient, UngerboeckSDKPackage.EventsModel @event)
    {
      Task<UngerboeckSDKPackage.EventsModel> eventTask = PutAsync(USISDKClient, $"Events/{@event.Organization}/{@event.EventID}", @event);
      return eventTask.Result;
    }

    public static UngerboeckSDKPackage.ExhibitorsModel GetExhibitor(HttpClient USISDKClient, string astrOrgCode, int aintExhibitorID)
    {
      Task<UngerboeckSDKPackage.ExhibitorsModel> ExhibitorTask = GetAsync<UngerboeckSDKPackage.ExhibitorsModel>(USISDKClient,
          $"Exhibitors/{astrOrgCode}/{aintExhibitorID}");
      return ExhibitorTask.Result;
    }
    public static UngerboeckSDKPackage.ExhibitorsModel AddExhibitor(HttpClient USISDKClient, UngerboeckSDKPackage.ExhibitorsModel exhibitor)
    {
      Task<UngerboeckSDKPackage.ExhibitorsModel> ExhibitorTask = PostAsync(USISDKClient, "Exhibitors", exhibitor);
      return ExhibitorTask.Result;
    }

    public static UngerboeckSDKPackage.ExhibitorsModel UpdateExhibitor(HttpClient USISDKClient, UngerboeckSDKPackage.ExhibitorsModel exhibitor)
    {
      Task<UngerboeckSDKPackage.ExhibitorsModel> ExhibitorTask = PutAsync(USISDKClient, $"Exhibitors/{exhibitor.OrganizationCode}/{exhibitor.ExhibitorID}", exhibitor);
      return ExhibitorTask.Result;
    }

    public static UngerboeckSDKPackage.OpportunitiesModel GetOpportunity(HttpClient USISDKClient, string astrOrgCode, string astrAccountCode, int aintSequenceNumber)
    {
      Task<UngerboeckSDKPackage.OpportunitiesModel> opportunityTask = GetAsync<UngerboeckSDKPackage.OpportunitiesModel>(USISDKClient,
          $"Opportunities/{astrOrgCode}/{astrAccountCode}/{aintSequenceNumber}");
      return opportunityTask.Result;
    }

    public static UngerboeckSDKPackage.OpportunitiesModel AddOpportunity(HttpClient USISDKClient, UngerboeckSDKPackage.OpportunitiesModel opportunity)
    {
      Task<UngerboeckSDKPackage.OpportunitiesModel> opportunityTask = PostAsync(USISDKClient, "Opportunities", opportunity);
      return opportunityTask.Result;
    }

    public static UngerboeckSDKPackage.OpportunitiesModel UpdateOpportunity(HttpClient USISDKClient, UngerboeckSDKPackage.OpportunitiesModel opportunity)
    {
      Task<UngerboeckSDKPackage.OpportunitiesModel> opportunityTask = PutAsync(USISDKClient, $"Opportunities/{opportunity.Organization}/{opportunity.Account}/{opportunity.SequenceNumber}", opportunity);
      return opportunityTask.Result;
    }

    public static async Task AwaitDeleteOpportunity(HttpClient USISDKClient, string astrOrgCode, string astrAccountCode, int aintSequenceNumber)
    {
      HttpResponseMessage response = await USISDKClient.DeleteAsync(
          $"{USISDKClient.BaseAddress}/api/v1/Opportunities/{astrOrgCode}/{astrAccountCode}/{aintSequenceNumber}").ConfigureAwait(false);
      APIUtil.SuccessResponse(response);
    }
    public static UngerboeckSDKPackage.PaymentsModel GetPayment(HttpClient USISDKClient, string astrOrgCode, string astrAccountCode, int aintSequence)
    {
      Task<UngerboeckSDKPackage.PaymentsModel> paymentTask = GetAsync<UngerboeckSDKPackage.PaymentsModel>(USISDKClient,
          $"Payments/{astrOrgCode}/{astrAccountCode}/{aintSequence}");
      return paymentTask.Result;
    }

    public static UngerboeckSDKPackage.PaymentsModel AddPayment(HttpClient USISDKClient, UngerboeckSDKPackage.PaymentsModel payment)
    {
      Task<UngerboeckSDKPackage.PaymentsModel> paymentTask = PostAsync(USISDKClient, "Payments", payment);
      return paymentTask.Result;
    }

    public static UngerboeckSDKPackage.PaymentsModel UpdatePayment(HttpClient USISDKClient, UngerboeckSDKPackage.PaymentsModel payment)
    {
      Task<UngerboeckSDKPackage.PaymentsModel> paymentTask = PutAsync(USISDKClient, $"Payments/{payment.Organization}/{payment.Account}/{payment.Sequence}", payment);
      return paymentTask.Result;
    }

    public static FunctionCheckInsModel GetFunctionCheckIn(HttpClient USISDKClient, int aintSequence)
    {
      Task<FunctionCheckInsModel> functionCheckInTask
          = GetAsync<FunctionCheckInsModel>(USISDKClient,
              $"FunctionCheckIns/{aintSequence}");
      return functionCheckInTask.Result;
    }

    public static FunctionCheckInsModel AddFunctionCheckIn(HttpClient USISDKClient, FunctionCheckInsModel functionCheckIn)
    {
      Task<FunctionCheckInsModel> functionCheckInTask = PostAsync(USISDKClient, "FunctionCheckIns", functionCheckIn);
      return functionCheckInTask.Result;
    }

    public static FunctionCheckInsModel UpdateFunctionCheckIn(HttpClient USISDKClient, FunctionCheckInsModel functionCheckIn)
    {
      Task<UngerboeckSDKPackage.FunctionCheckInsModel> functionCheckInTask
          = PutAsync(USISDKClient, $"FunctionCheckIns/{functionCheckIn.Sequence}", functionCheckIn);
      return functionCheckInTask.Result;
    }

    public static async Task AwaitDeleteFunctionCheckIn(HttpClient USISDKClient, int sequence)
    {
      HttpResponseMessage response = await USISDKClient.DeleteAsync($"{USISDKClient.BaseAddress}/api/v1/FunctionCheckIns/{sequence}").ConfigureAwait(false);
      APIUtil.SuccessResponse(response);
    }

    public static UngerboeckSDKPackage.RegistrationOrderItemsModel GetRegistrationOrderItem(HttpClient USISDKClient, string astrOrgCode, int aintOrderNumber, int aintOrderLineNumber)
    {
      Task<UngerboeckSDKPackage.RegistrationOrderItemsModel> registrationOrderItemTask = GetAsync<UngerboeckSDKPackage.RegistrationOrderItemsModel>(USISDKClient,
          $"RegistrationOrderItems/{astrOrgCode}/{aintOrderNumber}/{aintOrderLineNumber}");
      return registrationOrderItemTask.Result;
    }

    public static UngerboeckSDKPackage.RegistrationOrderItemsModel AddRegistrationOrderItem(HttpClient USISDKClient, UngerboeckSDKPackage.RegistrationOrderItemsModel registrationOrderItem)
    {
      Task<UngerboeckSDKPackage.RegistrationOrderItemsModel> registrationOrderItemTask = PostAsync(USISDKClient, "RegistrationOrderItems", registrationOrderItem);
      return registrationOrderItemTask.Result;
    }

    public static UngerboeckSDKPackage.RegistrationOrderItemsModel UpdateRegistrationOrderItem(HttpClient USISDKClient, UngerboeckSDKPackage.RegistrationOrderItemsModel registrationOrderItem)
    {
      Task<UngerboeckSDKPackage.RegistrationOrderItemsModel> registrationOrderItemTask = PutAsync(USISDKClient,
          $"RegistrationOrderItems/{registrationOrderItem.OrganizationCode}/{registrationOrderItem.OrderNumber}/{registrationOrderItem.OrderLineNumber}", registrationOrderItem);
      return registrationOrderItemTask.Result;
    }
    public static async Task AwaitDeleteRegistrationOrderItem(HttpClient USISDKClient, string astrOrgCode, int aintOrderNumber, int aintOrderLineNumber)
    {
      HttpResponseMessage response = await USISDKClient.DeleteAsync(
          $"{USISDKClient.BaseAddress}/api/v1/RegistrationOrderItems/{astrOrgCode}/{aintOrderNumber}/{aintOrderLineNumber}").ConfigureAwait(false);
      APIUtil.SuccessResponse(response);
    }
    public static UngerboeckSDKPackage.RegistrationOrdersModel GetRegistrationOrder(HttpClient USISDKClient, string astrOrgCode, int aintOrderNumber)
    {
      Task<UngerboeckSDKPackage.RegistrationOrdersModel> registrationOrderTask =
          GetAsync<UngerboeckSDKPackage.RegistrationOrdersModel>(USISDKClient,
          $"RegistrationOrders/{astrOrgCode}/{aintOrderNumber}");
      return registrationOrderTask.Result;
    }

    public static UngerboeckSDKPackage.RegistrationOrdersModel AddRegistrationOrder(HttpClient USISDKClient, UngerboeckSDKPackage.RegistrationOrdersModel registrationOrder)
    {
      Task<UngerboeckSDKPackage.RegistrationOrdersModel> registrationOrderTask = PostAsync(USISDKClient, "RegistrationOrders", registrationOrder);
      return registrationOrderTask.Result;
    }

    public static UngerboeckSDKPackage.RegistrationOrdersModel UpdateRegistrationOrder(HttpClient USISDKClient, UngerboeckSDKPackage.RegistrationOrdersModel registrationOrder)
    {
      Task<UngerboeckSDKPackage.RegistrationOrdersModel> registrationOrderTask = PutAsync(USISDKClient,
              $"RegistrationOrders/{registrationOrder.OrganizationCode}/{registrationOrder.OrderNumber}", registrationOrder);
      return registrationOrderTask.Result;
    }

    public static UngerboeckSDKPackage.RegistrationOrdersModel CalculateTaxesRegistrationOrder(HttpClient USISDKClient, UngerboeckSDKPackage.RegistrationOrdersModel registrationOrder)
    {
      Task<UngerboeckSDKPackage.RegistrationOrdersModel> registrationOrderTask = PostAsync(USISDKClient, "RegistrationOrders/CalculateTaxes", registrationOrder);
      return registrationOrderTask.Result;
    }

    public static async Task AwaitMoveRegistrationOrder(HttpClient USISDKClient, UngerboeckSDKPackage.MoveOrderModel moveRegistrationOrderInfo)
    {
      HttpResponseMessage response = await USISDKClient.PutAsJsonAsync(
          $"{USISDKClient.BaseAddress}/api/v1/RegistrationOrders/MoveOrder", moveRegistrationOrderInfo).ConfigureAwait(false);
      APIUtil.SuccessResponse(response);
    }

    public static async Task AwaitMoveServiceOrder(HttpClient USISDKClient, UngerboeckSDKPackage.MoveOrderModel moveServiceOrderInfo)
    {
      HttpResponseMessage response = await USISDKClient.PutAsJsonAsync(
          $"{USISDKClient.BaseAddress}/api/v1/ServiceOrders/MoveOrder", moveServiceOrderInfo).ConfigureAwait(false);
      APIUtil.SuccessResponse(response);
    }

    public static IEnumerable<UngerboeckSDKPackage.MoveOrdersBulkErrorsModel> MoveRegistrationOrdersBulk(HttpClient USISDKClient, UngerboeckSDKPackage.MoveOrdersBulkModel moveBulkRegistrationOrdersInfo)
    {
      Task<IEnumerable<UngerboeckSDKPackage.MoveOrdersBulkErrorsModel>> moveOrdersBulkErrorsTask =
          PutAsyncBulk<IEnumerable<UngerboeckSDKPackage.MoveOrdersBulkErrorsModel>, UngerboeckSDKPackage.MoveOrdersBulkModel>
           (USISDKClient, "RegistrationOrders/MoveOrdersBulk", moveBulkRegistrationOrdersInfo);

      return moveOrdersBulkErrorsTask.Result;
    }

    public static IEnumerable<UngerboeckSDKPackage.MoveOrdersBulkErrorsModel> MoveServiceOrdersBulk(HttpClient USISDKClient, UngerboeckSDKPackage.MoveOrdersBulkModel moveBulkServiceOrdersInfo)
    {
      Task<IEnumerable<UngerboeckSDKPackage.MoveOrdersBulkErrorsModel>> moveOrdersBulkErrorsTask =
          PutAsyncBulk<IEnumerable<UngerboeckSDKPackage.MoveOrdersBulkErrorsModel>, UngerboeckSDKPackage.MoveOrdersBulkModel>
           (USISDKClient, "ServiceOrders/MoveOrdersBulk", moveBulkServiceOrdersInfo);
      return moveOrdersBulkErrorsTask.Result;
    }


    public static UngerboeckSDKPackage.MembershipOrdersModel GetMembershipOrder(HttpClient USISDKClient, string astrOrgCode, int aintOrderNumber)
    {
      Task<UngerboeckSDKPackage.MembershipOrdersModel> membershipOrderTask
          = GetAsync<UngerboeckSDKPackage.MembershipOrdersModel>(USISDKClient,
              $"MembershipOrders/{astrOrgCode}/{aintOrderNumber}");
      return membershipOrderTask.Result;
    }

    public static UngerboeckSDKPackage.MembershipOrdersModel AddMembershipOrder(HttpClient USISDKClient, UngerboeckSDKPackage.MembershipOrdersModel membershipOrder)
    {
      Task<UngerboeckSDKPackage.MembershipOrdersModel> MembershipOrderTask = PostAsync(USISDKClient, "MembershipOrders", membershipOrder);
      return MembershipOrderTask.Result;
    }

    public static UngerboeckSDKPackage.MembershipOrdersModel UpdateMembershipOrder(HttpClient USISDKClient, UngerboeckSDKPackage.MembershipOrdersModel membershipOrder)
    {
      Task<UngerboeckSDKPackage.MembershipOrdersModel> membershipOrderTask
          = PutAsync(USISDKClient, $"MembershipOrders/{membershipOrder.OrganizationCode}/{membershipOrder.OrderNumber}", membershipOrder);
      return membershipOrderTask.Result;
    }

    public static async Task AwaitPrepareMembershipOrderForInvoicing(HttpClient USISDKClient, string astrOrgCode, int aintOrderNumber)
    {
      HttpResponseMessage response = await USISDKClient.PutAsync($"{USISDKClient.BaseAddress}/api/v1/MembershipOrders/{astrOrgCode}/{aintOrderNumber}/PrepareMembershipOrderForInvoicing", null).ConfigureAwait(false);
      APIUtil.SuccessResponse(response);
    }

    public static UngerboeckSDKPackage.MembershipOrderItemsModel GetMembershipOrderItem(HttpClient USISDKClient, string astrOrgCode, int aintOrderNumber, int aintOrderLineNumber)
    {
      Task<UngerboeckSDKPackage.MembershipOrderItemsModel> MembershipOrderItemTask
          = GetAsync<UngerboeckSDKPackage.MembershipOrderItemsModel>(USISDKClient, $"MembershipOrderItems/{astrOrgCode}/{aintOrderNumber}/{aintOrderLineNumber}");

      return MembershipOrderItemTask.Result;
    }

    public static UngerboeckSDKPackage.MembershipOrderItemsModel AddMembershipOrderItem(HttpClient USISDKClient, UngerboeckSDKPackage.MembershipOrderItemsModel membershipOrderItem)
    {
      Task<UngerboeckSDKPackage.MembershipOrderItemsModel> membershipOrderItemTask = PostAsync(USISDKClient, "MembershipOrderItems", membershipOrderItem);
      return membershipOrderItemTask.Result;
    }

    public static UngerboeckSDKPackage.MembershipOrderItemsModel UpdateMembershipOrderItem(HttpClient USISDKClient, UngerboeckSDKPackage.MembershipOrderItemsModel membershipOrderItem)
    {
      Task<UngerboeckSDKPackage.MembershipOrderItemsModel> membershipOrderItemTask = PutAsync(USISDKClient,
          $"MembershipOrderItems/{membershipOrderItem.OrganizationCode}/{membershipOrderItem.OrderNumber}/{membershipOrderItem.OrderLineNumber}", membershipOrderItem);
      return membershipOrderItemTask.Result;
    }

    public static async Task AwaitDeleteMembershipOrderItem(HttpClient USISDKClient, string orgCode, int orderNumberValue, int orderLineNumberValue)
    {
      HttpResponseMessage response = await USISDKClient.DeleteAsync(
          $"{USISDKClient.BaseAddress}/api/v1/MembershipOrderItems/{orgCode}/{orderNumberValue}/{orderLineNumberValue}").ConfigureAwait(false);
      APIUtil.SuccessResponse(response);
    }
    public static UngerboeckSDKPackage.NotesModel GetNote(HttpClient USISDKClient, string astrOrgCode, string astrType, string astrCode, decimal adecSequenceNumber)
    {
      Task<UngerboeckSDKPackage.NotesModel> noteTask = GetAsync<UngerboeckSDKPackage.NotesModel>(USISDKClient, $"Notes/{astrOrgCode}/{astrType}/{astrCode}/{adecSequenceNumber}");

      return noteTask.Result;
    }

    public static UngerboeckSDKPackage.NotesModel UpdateNote(HttpClient USISDKClient, UngerboeckSDKPackage.NotesModel note)
    {
      Task<UngerboeckSDKPackage.NotesModel> noteTask = PutAsync(USISDKClient, $"Notes/{note.OrganizationCode}/{note.Type}/{note.Code}/{note.SequenceNumber}", note);
      return noteTask.Result;
    }
    public static UngerboeckSDKPackage.NotesModel AddNote(HttpClient USISDKClient, UngerboeckSDKPackage.NotesModel note)
    {
      Task<UngerboeckSDKPackage.NotesModel> noteTask = PostAsync(USISDKClient, "Notes", note);
      return noteTask.Result;
    }

    public static async Task AwaitDeleteNote(HttpClient USISDKClient, string orgCode, string astrType, string astrCode, decimal adecSequenceNumber)
    {
      HttpResponseMessage response = await USISDKClient.DeleteAsync($"{USISDKClient.BaseAddress}/api/v1/Notes/{orgCode}/{astrType}/{astrCode}/{adecSequenceNumber}").ConfigureAwait(false);
      APIUtil.SuccessResponse(response);
    }
    public static UngerboeckSDKPackage.RelationshipsModel GetRelationship(HttpClient USISDKClient, string astrOrgCode, string astrMasterAccountCode, string astrSubordinateAccountCode, string astrRelationshipType)
    {
      Task<UngerboeckSDKPackage.RelationshipsModel> relationshipTask
          = GetAsync<UngerboeckSDKPackage.RelationshipsModel>(USISDKClient,
          $"Relationships/{astrOrgCode}/{astrMasterAccountCode}/{astrSubordinateAccountCode}/{astrRelationshipType}");
      return relationshipTask.Result;
    }
    public static UngerboeckSDKPackage.RelationshipsModel AddRelationship(HttpClient USISDKClient, UngerboeckSDKPackage.RelationshipsModel relationship)
    {
      Task<UngerboeckSDKPackage.RelationshipsModel> relationshipTask
        = PostAsync(USISDKClient, "Relationships", relationship);
      return relationshipTask.Result;
    }

    public static UngerboeckSDKPackage.RelationshipsModel UpdateRelationship(HttpClient USISDKClient, UngerboeckSDKPackage.RelationshipsModel relationship)
    {
      Task<UngerboeckSDKPackage.RelationshipsModel> relationshipTask
          = PutAsync(USISDKClient, $"Relationships/{relationship.MasterOrganizationCode}/{relationship.MasterAccountCode}/{relationship.SubordinateAccountCode}/{relationship.RelationshipType}", relationship);
      return relationshipTask.Result;
    }

    public static async Task AwaitDeleteRelationship(HttpClient USISDKClient, string astrOrgCode, string astrMasterAccountCode, string astrSubordinateAccountCode, string astrRelationshipType)
    {
      HttpResponseMessage response = await USISDKClient.DeleteAsync(
          $"{USISDKClient.BaseAddress}/api/v1/Relationships/{astrOrgCode}/{astrMasterAccountCode}/{astrSubordinateAccountCode}/{astrRelationshipType}").ConfigureAwait(false);
      APIUtil.SuccessResponse(response);
    }
    public static UngerboeckSDKPackage.RelationshipTypesModel GetRelationshipType(HttpClient USISDKClient, string astrOrgCode, string astrCode)
    {
      Task<UngerboeckSDKPackage.RelationshipTypesModel> relationshipTypeTask = GetAsync<UngerboeckSDKPackage.RelationshipTypesModel>(USISDKClient, $"RelationshipTypes/{astrOrgCode}/{astrCode}");
      return relationshipTypeTask.Result;
    }
    [Obsolete("Use GetRelationshipType to get a single item or GetSearchList for a list")]
    public static UngerboeckSDKPackage.RelationshipTypesModel GetRelationshipTypes(HttpClient USISDKClient, string astrOrgCode, string astrCode)
    {
      Task<UngerboeckSDKPackage.RelationshipTypesModel> relationshipTypeTask = GetAsync<UngerboeckSDKPackage.RelationshipTypesModel>(USISDKClient, $"RelationshipTypes/{astrOrgCode}/{astrCode}");
      return relationshipTypeTask.Result;
    }
    public static UngerboeckSDKPackage.RequisitionApprovalModel GetRequisitionApproval(HttpClient USISDKClient, string astrOrgCode, int aintNumber, int aintLine, int aintSequence)
    {
      Task<UngerboeckSDKPackage.RequisitionApprovalModel> requisitionApprovalTask = GetAsync<UngerboeckSDKPackage.RequisitionApprovalModel>(USISDKClient, $"RequisitionApproval/{astrOrgCode}/{aintNumber}/{aintLine}/{aintSequence}");
      return requisitionApprovalTask.Result;
    }

    public static UngerboeckSDKPackage.PurchaseOrderApprovalModel GetPurchaseOrderApproval(HttpClient USISDKClient, string astrOrgCode, int aintNumber, int aintItemSequence, int aintSequence)
    {
      Task<UngerboeckSDKPackage.PurchaseOrderApprovalModel> purchaseOrderApprovalTask = GetAsync<UngerboeckSDKPackage.PurchaseOrderApprovalModel>(USISDKClient, $"PurchaseOrderApproval/{astrOrgCode}/{aintNumber}/{aintItemSequence}/{aintSequence}");
      return purchaseOrderApprovalTask.Result;
    }

    public static UngerboeckSDKPackage.ServiceOrderItemsModel GetServiceOrderItem(HttpClient USISDKClient, string astrOrgCode, int aintOrderNumber, int aintOrderLineNumber)
    {
      Task<UngerboeckSDKPackage.ServiceOrderItemsModel> serviceOrderItemTask =
          GetAsync<UngerboeckSDKPackage.ServiceOrderItemsModel>(USISDKClient,
          $"ServiceOrderItems/{astrOrgCode}/{aintOrderNumber}/{aintOrderLineNumber}");

      return serviceOrderItemTask.Result;
    }

    public static UngerboeckSDKPackage.ServiceOrderItemsModel UpdateServiceOrderItem(HttpClient USISDKClient, UngerboeckSDKPackage.ServiceOrderItemsModel serviceOrderItem)
    {
      Task<UngerboeckSDKPackage.ServiceOrderItemsModel> serviceOrderItemTask =
          PutAsync(USISDKClient, $"ServiceOrderItems/{serviceOrderItem.OrganizationCode}/{serviceOrderItem.OrderNumber}/{serviceOrderItem.OrderLineNumber}", serviceOrderItem);
      return serviceOrderItemTask.Result;
    }
    public static UngerboeckSDKPackage.ServiceOrderItemsModel AddServiceOrderItem(HttpClient USISDKClient, UngerboeckSDKPackage.ServiceOrderItemsModel serviceOrderItem)
    {
      Task<UngerboeckSDKPackage.ServiceOrderItemsModel> serviceOrderItemTask
          = PostAsync(USISDKClient, "ServiceOrderItems", serviceOrderItem);
      return serviceOrderItemTask.Result;
    }

    public static async Task AwaitDeleteServiceOrderItem(HttpClient USISDKClient, string orgCode, int orderNumberValue, int orderLineNumberValue)
    {
      HttpResponseMessage response = await USISDKClient.DeleteAsync($"{USISDKClient.BaseAddress}/api/v1/ServiceOrderItems/{orgCode}/{orderNumberValue}/{orderLineNumberValue}").ConfigureAwait(false);
      APIUtil.SuccessResponse(response);
    }

    public static UngerboeckSDKPackage.ServiceOrdersModel GetServiceOrder(HttpClient USISDKClient, string astrOrgCode, int aintOrderNumber)
    {
      Task<UngerboeckSDKPackage.ServiceOrdersModel> serviceOrderTask = GetAsync<UngerboeckSDKPackage.ServiceOrdersModel>(USISDKClient,
          $"ServiceOrders/{astrOrgCode}/{aintOrderNumber}");

      return serviceOrderTask.Result;
    }

    public static UngerboeckSDKPackage.ServiceOrdersModel AddServiceOrder(HttpClient USISDKClient, UngerboeckSDKPackage.ServiceOrdersModel serviceOrder)
    {
      Task<UngerboeckSDKPackage.ServiceOrdersModel> serviceOrderTask = PostAsync(USISDKClient, "ServiceOrders", serviceOrder);
      return serviceOrderTask.Result;
    }

    public static UngerboeckSDKPackage.ServiceOrdersModel UpdateServiceOrder(HttpClient USISDKClient, UngerboeckSDKPackage.ServiceOrdersModel serviceOrder)
    {
      Task<UngerboeckSDKPackage.ServiceOrdersModel> serviceOrderTask = PutAsync(USISDKClient,
          $"ServiceOrders/{serviceOrder.OrganizationCode}/{serviceOrder.OrderNumber}", serviceOrder);
      return serviceOrderTask.Result;
    }

    public static async Task AwaitCompleteWorkOrders(HttpClient USISDKClient, string astrOrgCode, int aintOrderNumber)
    {
      HttpResponseMessage response = await USISDKClient.PutAsync(
          $"{USISDKClient.BaseAddress}/api/v1/ServiceOrders/{astrOrgCode}/{aintOrderNumber}/CompleteWorkOrders", null).ConfigureAwait(false);
      APIUtil.SuccessResponse(response);
    }
    public static UngerboeckSDKPackage.RequisitionsModel GetRequisition(HttpClient USISDKClient, string astrOrgCode, int aintNumber)
    {
      Task<UngerboeckSDKPackage.RequisitionsModel> resourceTask = GetAsync<UngerboeckSDKPackage.RequisitionsModel>(USISDKClient, $"Requisitions/{astrOrgCode}/{aintNumber}");

      return resourceTask.Result;
    }
    [Obsolete("Use GetRequisition to get a single item or GetSearchList for a list")]
    public static UngerboeckSDKPackage.RequisitionsModel GetRequisitions(HttpClient USISDKClient, string astrOrgCode, int aintNumber)
    {
      Task<UngerboeckSDKPackage.RequisitionsModel> resourceTask = GetAsync<UngerboeckSDKPackage.RequisitionsModel>(USISDKClient, $"Requisitions/{astrOrgCode}/{aintNumber}");

      return resourceTask.Result;
    }
    public static UngerboeckSDKPackage.RequisitionItemsModel GetRequisitionItem(HttpClient USISDKClient, string astrOrgCode, int aintNumber, int aintSequence)
    {
      Task<UngerboeckSDKPackage.RequisitionItemsModel> requisitionItemTask = GetAsync<UngerboeckSDKPackage.RequisitionItemsModel>(USISDKClient, $"RequisitionItems/{astrOrgCode}/{aintNumber}/{aintSequence}");

      return requisitionItemTask.Result;
    }
    [Obsolete("Use GeRequisitionItem to get a single item or GetSearchList for a list")]
    public static UngerboeckSDKPackage.RequisitionItemsModel GeRequisitionItems(HttpClient USISDKClient, string astrOrgCode, int aintNumber, int aintSequence)
    {
      Task<UngerboeckSDKPackage.RequisitionItemsModel> requisitionItemTask = GetAsync<UngerboeckSDKPackage.RequisitionItemsModel>(USISDKClient, $"RequisitionItems/{astrOrgCode}/{aintNumber}/{aintSequence}");

      return requisitionItemTask.Result;
    }
    public static UngerboeckSDKPackage.ResourcesModel GetResource(HttpClient USISDKClient, string astrOrgCode, int aintSequence)
    {
      Task<UngerboeckSDKPackage.ResourcesModel> resourceTask = GetAsync<UngerboeckSDKPackage.ResourcesModel>(USISDKClient, $"Resources/{astrOrgCode}/{aintSequence}");

      return resourceTask.Result;
    }
    [Obsolete("Use GetResource to get a single item or GetSearchList for a list")]
    public static UngerboeckSDKPackage.ResourcesModel GetResources(HttpClient USISDKClient, string astrOrgCode, int aintSequence)
    {
      Task<UngerboeckSDKPackage.ResourcesModel> resourceTask = GetAsync<UngerboeckSDKPackage.ResourcesModel>(USISDKClient, $"Resources/{astrOrgCode}/{aintSequence}");

      return resourceTask.Result;
    }
    public static UngerboeckSDKPackage.SpacesModel GetSpace(HttpClient USISDKClient, string astrOrgCode, string astrCode)
    {
      Task<UngerboeckSDKPackage.SpacesModel> spaceTask = GetAsync<UngerboeckSDKPackage.SpacesModel>(USISDKClient, $"Spaces/{astrOrgCode}/{astrCode}");

      return spaceTask.Result;
    }
    [Obsolete("Use GetSpace to get a single item or GetSearchList for a list")]
    public static UngerboeckSDKPackage.SpacesModel GetSpaces(HttpClient USISDKClient, string astrOrgCode, string astrCode)
    {
      Task<UngerboeckSDKPackage.SpacesModel> spaceTask = GetAsync<UngerboeckSDKPackage.SpacesModel>(USISDKClient, $"Spaces/{astrOrgCode}/{astrCode}");

      return spaceTask.Result;
    }
    public static UngerboeckSDKPackage.SpacesModel AddSpace(HttpClient USISDKClient, UngerboeckSDKPackage.SpacesModel space)
    {
      Task<UngerboeckSDKPackage.SpacesModel> spaceTask = PostAsync(USISDKClient, "Spaces", space);
      return spaceTask.Result;
    }

    public static UngerboeckSDKPackage.SpacesModel UpdateSpace(HttpClient USISDKClient, UngerboeckSDKPackage.SpacesModel space)
    {
      Task<UngerboeckSDKPackage.SpacesModel> spaceTask = PutAsync(USISDKClient, $"Spaces/{space.Organization}/{space.Code}", space);
      return spaceTask.Result;
    }
    public static UngerboeckSDKPackage.SetupsModel GetSetup(HttpClient USISDKClient, string astrOrgCode, string astrCode)
    {
      Task<UngerboeckSDKPackage.SetupsModel> setupTask = GetAsync<UngerboeckSDKPackage.SetupsModel>(USISDKClient, $"Setups/{astrOrgCode}/{astrCode}");
      return setupTask.Result;
    }
    [Obsolete("Use GetSetup to get a single item or GetSearchList for a list")]
    public static UngerboeckSDKPackage.SetupsModel GetSetups(HttpClient USISDKClient, string astrOrgCode, string astrCode)
    {
      Task<UngerboeckSDKPackage.SetupsModel> setupTask = GetAsync<UngerboeckSDKPackage.SetupsModel>(USISDKClient, $"Setups/{astrOrgCode}/{astrCode}");
      return setupTask.Result;
    }
    public static UngerboeckSDKPackage.SpaceSetupsModel GetSpaceSetup(HttpClient USISDKClient, string astrOrgCode, string astrSpace, string astrCode)
    {
      Task<UngerboeckSDKPackage.SpaceSetupsModel> spaceSetupTask = GetAsync<UngerboeckSDKPackage.SpaceSetupsModel>(USISDKClient, $"SpaceSetups/{astrOrgCode}/{astrSpace}/{astrCode}");
      return spaceSetupTask.Result;
    }
    [Obsolete("Use GetSpaceSetup to get a single item or GetSearchList for a list")]
    public static UngerboeckSDKPackage.SpaceSetupsModel GetSpaceSetups(HttpClient USISDKClient, string astrOrgCode, string astrSpace, string astrCode)
    {
      Task<UngerboeckSDKPackage.SpaceSetupsModel> spaceSetupTask = GetAsync<UngerboeckSDKPackage.SpaceSetupsModel>(USISDKClient, $"SpaceSetups/{astrOrgCode}/{astrSpace}/{astrCode}");
      return spaceSetupTask.Result;
    }
    public static UngerboeckSDKPackage.SpaceFeaturesModel GetSpaceFeature(HttpClient USISDKClient, string astrOrgCode, string astrCode)
    {
      Task<UngerboeckSDKPackage.SpaceFeaturesModel> spaceFeatureTask = GetAsync<UngerboeckSDKPackage.SpaceFeaturesModel>(USISDKClient, $"SpaceFeatures/{astrOrgCode}/{astrCode}");
      return spaceFeatureTask.Result;
    }
    [Obsolete("Use GetSpaceFeature to get a single item or GetSearchList for a list")]
    public static UngerboeckSDKPackage.SpaceFeaturesModel GetSpaceFeatures(HttpClient USISDKClient, string astrOrgCode, string astrCode)
    {
      Task<UngerboeckSDKPackage.SpaceFeaturesModel> spaceFeatureTask = GetAsync<UngerboeckSDKPackage.SpaceFeaturesModel>(USISDKClient, $"SpaceFeatures/{astrOrgCode}/{astrCode}");
      return spaceFeatureTask.Result;
    }
    public static UngerboeckSDKPackage.TransactionMethodsModel GetTransactionMethod(HttpClient USISDKClient, string astrOrgCode, string astrCode)
    {
      Task<UngerboeckSDKPackage.TransactionMethodsModel> transactionMethodTask = GetAsync<UngerboeckSDKPackage.TransactionMethodsModel>(USISDKClient, $"TransactionMethods/{astrOrgCode}/{astrCode}");

      return transactionMethodTask.Result;
    }
    [Obsolete("Use GetTransactionMethod to get a single item or GetSearchList for a list")]
    public static UngerboeckSDKPackage.TransactionMethodsModel GetTransactionMethods(HttpClient USISDKClient, string astrOrgCode, string astrCode)
    {
      Task<UngerboeckSDKPackage.TransactionMethodsModel> transactionMethodTask = GetAsync<UngerboeckSDKPackage.TransactionMethodsModel>(USISDKClient, $"TransactionMethods/{astrOrgCode}/{astrCode}");

      return transactionMethodTask.Result;
    }
    public static UngerboeckSDKPackage.UserDefinedFieldsModel GetUserDefinedField(HttpClient USISDKClient, string astrOrgCode, string astrIssueOpportunityClass, string astrIssueOpportunityType, int aintSequenceNumber)
    {
      Task<UngerboeckSDKPackage.UserDefinedFieldsModel> userDefinedFieldTask = GetAsync<UngerboeckSDKPackage.UserDefinedFieldsModel>(USISDKClient, $"UserDefinedFields/{astrOrgCode}/{astrIssueOpportunityClass}/{astrIssueOpportunityType}/{aintSequenceNumber}");

      return userDefinedFieldTask.Result;
    }
    [Obsolete("Use GetUserDefinedField to get a single item or GetSearchList for a list")]
    public static UngerboeckSDKPackage.UserDefinedFieldsModel GetUserDefinedFields(HttpClient USISDKClient, string astrOrgCode, string astrIssueOpportunityClass, string astrIssueOpportunityType, int aintSequenceNumber)
    {
      Task<UngerboeckSDKPackage.UserDefinedFieldsModel> userDefinedFieldTask = GetAsync<UngerboeckSDKPackage.UserDefinedFieldsModel>(USISDKClient, $"UserDefinedFields/{astrOrgCode}/{astrIssueOpportunityClass}/{astrIssueOpportunityType}/{aintSequenceNumber}");

      return userDefinedFieldTask.Result;
    }
    public static UngerboeckSDKPackage.ValidationEntriesModel GetValidationEntry(HttpClient USISDKClient, string astrOrgCode, int aintValidationTableID, int aintSequenceNumber)
    {
      Task<UngerboeckSDKPackage.ValidationEntriesModel> validationEntryTask = GetAsync<UngerboeckSDKPackage.ValidationEntriesModel>(USISDKClient, $"ValidationEntries/{astrOrgCode}/{aintValidationTableID}/{aintSequenceNumber}");

      return validationEntryTask.Result;
    }
    [Obsolete("Use GetValidationEntry to get a single item or GetSearchList for a list")]
    public static UngerboeckSDKPackage.ValidationEntriesModel GetValidationEntries(HttpClient USISDKClient, string astrOrgCode, int aintValidationTableID, int aintSequenceNumber)
    {
      Task<UngerboeckSDKPackage.ValidationEntriesModel> validationEntryTask = GetAsync<UngerboeckSDKPackage.ValidationEntriesModel>(USISDKClient, $"ValidationEntries/{astrOrgCode}/{aintValidationTableID}/{aintSequenceNumber}");

      return validationEntryTask.Result;
    }
    public static UngerboeckSDKPackage.WorkOrdersModel GetWorkOrder(HttpClient USISDKClient, string astrOrgCode, int aintOrder, string astrDepartment)
    {
      Task<UngerboeckSDKPackage.WorkOrdersModel> workOrderTask = GetAsync<UngerboeckSDKPackage.WorkOrdersModel>(USISDKClient, $"WorkOrders/{astrOrgCode}/{aintOrder}/{astrDepartment}");

      return workOrderTask.Result;
    }
    public static UngerboeckSDKPackage.WorkOrderItemsModel GetWorkOrderItem(HttpClient USISDKClient, string astrOrgCode, int aintOrderNbr, int aintOrderLineNbr)
    {
      Task<UngerboeckSDKPackage.WorkOrderItemsModel> workOrderItemTask =
          GetAsync<UngerboeckSDKPackage.WorkOrderItemsModel>(USISDKClient, $"WorkOrderItems/{astrOrgCode}/{aintOrderNbr}/{aintOrderLineNbr}");
      return workOrderItemTask.Result;
    }
    [Obsolete("Use GetWorkOrderItem to get a single item or GetSearchList for a list")]
    public static UngerboeckSDKPackage.WorkOrderItemsModel GetWorkOrderItems(HttpClient USISDKClient, string astrOrgCode, int aintOrderNbr, int aintOrderLineNbr)
    {
      Task<UngerboeckSDKPackage.WorkOrderItemsModel> workOrderItemTask =
          GetAsync<UngerboeckSDKPackage.WorkOrderItemsModel>(USISDKClient, $"WorkOrderItems/{astrOrgCode}/{aintOrderNbr}/{aintOrderLineNbr}");
      return workOrderItemTask.Result;
    }
    public static UngerboeckSDKPackage.WebhooksModel GetWebHook(HttpClient USISDKClient, string astrOrgCode, int aintSequenceNumber)
    {
      Task<UngerboeckSDKPackage.WebhooksModel> webHookTask = GetAsync<UngerboeckSDKPackage.WebhooksModel>(USISDKClient, $"Webhooks/{astrOrgCode}/{aintSequenceNumber}");
      return webHookTask.Result;
    }
    [Obsolete("Use GetWebHook to get a single item or GetSearchList for a list")]
    public static UngerboeckSDKPackage.WebhooksModel GetWebHooks(HttpClient USISDKClient, string astrOrgCode, int aintSequenceNumber)
    {
      Task<UngerboeckSDKPackage.WebhooksModel> webHookTask = GetAsync<UngerboeckSDKPackage.WebhooksModel>(USISDKClient, $"Webhooks/{astrOrgCode}/{aintSequenceNumber}");
      return webHookTask.Result;
    }

    public static UngerboeckSDKPackage.UsersModel GetUser(HttpClient USISDKClient, string ID)
    {
      Task<UngerboeckSDKPackage.UsersModel> userTask = GetAsync<UngerboeckSDKPackage.UsersModel>(USISDKClient, $"Users/{ID}");
      return userTask.Result;
    }

    public static UngerboeckSDKPackage.UsersModel AddUser(HttpClient USISDKClient, UngerboeckSDKPackage.UsersModel user)
    {
      Task<UngerboeckSDKPackage.UsersModel> userTask = PostAsync(USISDKClient, "Users", user);
      return userTask.Result;
    }

    public static UngerboeckSDKPackage.UsersModel UpdateUser(HttpClient USISDKClient, UngerboeckSDKPackage.UsersModel user)
    {
      Task<UngerboeckSDKPackage.UsersModel> userTask = PutAsync(USISDKClient, $"Users/{user.ID}", user);
      return userTask.Result;
    }

    /// <summary>
    /// This will send out an email to the user.  The user information will not be modified.
    /// </summary>    
    public static HttpResponseMessage SendActivateUserEmail(HttpClient USISDKClient, string astrOrgCode, string astrID)
    {
      Task<HttpResponseMessage> response = USISDKClient.PutAsync($"{USISDKClient.BaseAddress}/api/v1/Users/{astrID}/ResetPassword", null);
      return response.Result;
    }

    /// <summary>
    /// This will make a new user from the user with the User ID of the sourceID parameter.
    /// </summary>  
    public static UsersModel CopyUser(HttpClient USISDKClient, string sourceID, UngerboeckSDKPackage.Copy.Users user)
    {
      Task<UsersModel> response = PutAsync<UngerboeckSDKPackage.Copy.Users, UsersModel>(USISDKClient, $"Users/{sourceID}/Copy", user);
      return response.Result;
    }

    public static UngerboeckSDKPackage.RolesModel GetRole(HttpClient USISDKClient, string ID)
    {
      Task<UngerboeckSDKPackage.RolesModel> RoleTask = GetAsync<UngerboeckSDKPackage.RolesModel>(USISDKClient, $"Roles/{ID}");
      return RoleTask.Result;
    }

    public static UngerboeckSDKPackage.RolesModel AddRole(HttpClient USISDKClient, UngerboeckSDKPackage.RolesModel Role)
    {
      Task<UngerboeckSDKPackage.RolesModel> RoleTask = PostAsync(USISDKClient, "Roles", Role);
      return RoleTask.Result;
    }

    public static UngerboeckSDKPackage.RolesModel UpdateRole(HttpClient USISDKClient, UngerboeckSDKPackage.RolesModel Role)
    {
      Task<UngerboeckSDKPackage.RolesModel> RoleTask = PutAsync(USISDKClient, $"Roles/{Role.ID}", Role);
      return RoleTask.Result;
    }

    /// <summary>
    /// Get a User Role.  
    /// </summary>
    /// <param name="USISDKClient"></param>
    /// <param name="user">The User ID</param>
    /// <param name="role">The Role ID</param>
    /// <returns>A single UserRole model</returns>
    public static UngerboeckSDKPackage.UserRolesModel GetUserRole(HttpClient USISDKClient, string user, string role)
    {
      Task<UngerboeckSDKPackage.UserRolesModel> UserRoleTask = GetAsync<UngerboeckSDKPackage.UserRolesModel>(USISDKClient, $"UserRoles/{user}/{role}");
      return UserRoleTask.Result;
    }

    /// <summary>
    /// Add a user to a role.  You can do more than one.  You can include a comma delimited list of User ID's or Role ID's, but not both.
    /// </summary>
    /// <param name="USISDKClient"></param>
    /// <param name="UserRole">A UserRoleModel representing a new relationship. You can include a comma delimited list of User ID's or Role ID's, but not both.</param>
    /// <returns></returns>
    public static string AddUserRole(HttpClient USISDKClient, UngerboeckSDKPackage.UserRolesModel UserRole)
    {
      Task<string> UserRoleTask = PostAsync<UserRolesModel, string>(USISDKClient, "UserRoles", UserRole);
      return UserRoleTask.Result;
    }

    /// <summary>
    /// Remove a User from a Role.  
    /// </summary>
    /// <param name="USISDKClient"></param>
    /// <param name="user">The User ID.  You can include a comma delimited list of User ID's or Role ID's, but not both.</param>
    /// <param name="role">The Role ID.  You can include a comma delimited list of User ID's or Role ID's, but not both.</param>
    /// <returns>A single UserRole model</returns>
    public static string DeleteUserRole(HttpClient USISDKClient, string user, string role)
    {
      Task<string> UserRoleTask = DeleteAsync<string>(USISDKClient, $"UserRoles/{user}/{role}");
      return UserRoleTask.Result;
    }

    public static UngerboeckSDKPackage.UngerboeckAuthenticationCheck CheckUngerboeckUserAuthentication(HttpClient USISDKClient, string userID, string password, string ungerboeckURI, string userIDToCheck, string userPasswordToCheck)
    {
      // This endpoint can be used to check authorization of a user (i.e., you wish to make a login/access page for users for your website that also have Ungerboeck users)
      APIUtil.InitializeAPIClient(USISDKClient, ungerboeckURI, userID, password);

      USISDKClient.DefaultRequestHeaders.Add("CheckAuthentication", Convert.ToBase64String(
          System.Text.ASCIIEncoding.ASCII.GetBytes($"{userIDToCheck}:{userPasswordToCheck}")));

      Task<UngerboeckSDKPackage.UngerboeckAuthenticationCheck> ungerboeckAuthenticationCheckTask = GetAsync<UngerboeckSDKPackage.UngerboeckAuthenticationCheck>(USISDKClient, "sdk_initialize");

      if ((ungerboeckAuthenticationCheckTask == null))
      {
        throw new Exception("UngerboeckAuthenticationCheck is nothing"); // This likely will never happen
      }
      else
      {
        UngerboeckSDKPackage.UngerboeckAuthenticationCheck ungerboeckAuthenticationCheck = ungerboeckAuthenticationCheckTask.Result;
        return ungerboeckAuthenticationCheck;
      }
    }


    public static UngerboeckSDKPackage.SessionProposalsModel GetSessionProposal(HttpClient USISDKClient, string astrOrgCode, int aintSessionProposalId)
    {
      Task<UngerboeckSDKPackage.SessionProposalsModel> sessionProposalTask =
          GetAsync<UngerboeckSDKPackage.SessionProposalsModel>(USISDKClient, $"SessionProposals/{astrOrgCode}/{aintSessionProposalId}");
      return sessionProposalTask.Result;
    }

    public static UngerboeckSDKPackage.SessionProposalsModel AddSessionProposal(HttpClient USISDKClient, UngerboeckSDKPackage.SessionProposalsModel sessionProposal)
    {
      Task<UngerboeckSDKPackage.SessionProposalsModel> sessionProposalTask = PostAsync(USISDKClient, "SessionProposals", sessionProposal);
      return sessionProposalTask.Result;
    }

    public static UngerboeckSDKPackage.SessionProposalsModel UpdateSessionProposal(HttpClient USISDKClient, UngerboeckSDKPackage.SessionProposalsModel sessionProposal)
    {
      Task<UngerboeckSDKPackage.SessionProposalsModel> sessionProposalTask = PutAsync(USISDKClient,
              $"SessionProposals/{sessionProposal.OrganizationCode}/{sessionProposal.ID}", sessionProposal);
      return sessionProposalTask.Result;
    }

    public static async Task AwaitDeleteSessionProposal(HttpClient USISDKClient, string astrOrgCode, int aintSessionProposalId)
    {
      HttpResponseMessage response = await USISDKClient.DeleteAsync($"{USISDKClient.BaseAddress}/api/v1/SessionProposals/{astrOrgCode}/{aintSessionProposalId}").ConfigureAwait(false);
      APIUtil.SuccessResponse(response);
    }

    public static async Task AwaitAssignEvaluators(HttpClient USISDKClient, string astrOrgCode, int aintSessionProposalId, SessionProposalsAssignEvaluatorsModel sessionProposalAssignEvaluator)
    {
      HttpResponseMessage response = await USISDKClient.PutAsJsonAsync(
        $"{USISDKClient.BaseAddress}/api/v1/SessionProposals/{astrOrgCode}/{aintSessionProposalId}/AssignEvaluators", sessionProposalAssignEvaluator).ConfigureAwait(false);
      APIUtil.SuccessResponse(response);
    }


    public static UngerboeckSDKPackage.SessionProposalEvaluationsModel GetSessionProposalEvaluation(HttpClient USISDKClient, int aintSessionProposalEvaluationId)
    {
      Task<UngerboeckSDKPackage.SessionProposalEvaluationsModel> sessionProposalEvlTask =
          GetAsync<UngerboeckSDKPackage.SessionProposalEvaluationsModel>(USISDKClient,
          $"SessionProposalEvaluations/{aintSessionProposalEvaluationId}");
      return sessionProposalEvlTask.Result;
    }

    public static UngerboeckSDKPackage.SessionProposalEvaluationsModel AddSessionProposalEvaluation(HttpClient USISDKClient, UngerboeckSDKPackage.SessionProposalEvaluationsModel sessionProposalEvaluation)
    {
      Task<UngerboeckSDKPackage.SessionProposalEvaluationsModel> sessionProposalEvlTask = PostAsync(USISDKClient, "SessionProposalEvaluations", sessionProposalEvaluation);
      return sessionProposalEvlTask.Result;
    }

    public static UngerboeckSDKPackage.SessionProposalEvaluationsModel UpdateSessionProposalEvaluation(HttpClient USISDKClient, UngerboeckSDKPackage.SessionProposalEvaluationsModel sessionProposalEvaluation)
    {
      Task<UngerboeckSDKPackage.SessionProposalEvaluationsModel> sessionProposalEvlTask = PutAsync(USISDKClient,
              $"SessionProposalEvaluations/{sessionProposalEvaluation.SessionProposalEvaluationID}", sessionProposalEvaluation);
      return sessionProposalEvlTask.Result;
    }

    public static async Task AwaitDeleteSessionProposalEvaluation(HttpClient USISDKClient, int aintSessionProposalEvaluationId)
    {
      HttpResponseMessage response = await USISDKClient.DeleteAsync($"{USISDKClient.BaseAddress}/api/v1/SessionProposalEvaluations/{aintSessionProposalEvaluationId}").ConfigureAwait(false);
      APIUtil.SuccessResponse(response);
    }

    public static UngerboeckSDKPackage.SessionProposalEvaluationCriteriaResponsesModel GetSessionProposalEvaluationCriteriaResponse(HttpClient USISDKClient, int aintSessionProposalEvaluationCriteriaResponseId)
    {
      Task<UngerboeckSDKPackage.SessionProposalEvaluationCriteriaResponsesModel> sessionProposalEvlRespTask =
          GetAsync<UngerboeckSDKPackage.SessionProposalEvaluationCriteriaResponsesModel>(USISDKClient,
          $"SessionProposalEvaluationCriteriaResponses/{aintSessionProposalEvaluationCriteriaResponseId}");
      return sessionProposalEvlRespTask.Result;
    }

    public static UngerboeckSDKPackage.SessionProposalEvaluationCriteriaResponsesModel AddSessionProposalEvaluationCriteriaResponse(HttpClient USISDKClient, UngerboeckSDKPackage.SessionProposalEvaluationCriteriaResponsesModel sessionProposalEvaluationCriteriaResponse)
    {
      Task<UngerboeckSDKPackage.SessionProposalEvaluationCriteriaResponsesModel> sessionProposalEvlRespTask =
        PostAsync(USISDKClient, "SessionProposalEvaluationCriteriaResponses", sessionProposalEvaluationCriteriaResponse);
      return sessionProposalEvlRespTask.Result;
    }

    public static UngerboeckSDKPackage.SessionProposalEvaluationCriteriaResponsesModel UpdateSessionProposalEvaluationCriteriaResponse(HttpClient USISDKClient, UngerboeckSDKPackage.SessionProposalEvaluationCriteriaResponsesModel sessionProposalEvaluationCriteriaResponse)
    {
      Task<UngerboeckSDKPackage.SessionProposalEvaluationCriteriaResponsesModel> sessionProposalEvlRespTask =
        PutAsync(USISDKClient,
                 $"SessionProposalEvaluationCriteriaResponses/{sessionProposalEvaluationCriteriaResponse.Id}", sessionProposalEvaluationCriteriaResponse);
      return sessionProposalEvlRespTask.Result;
    }

    public static async Task AwaitDeleteSessionProposalEvaluationCriteriaResponse(HttpClient USISDKClient, int aintSessionProposalEvaluationCriteriaResponseId)
    {
      HttpResponseMessage response = await USISDKClient.DeleteAsync($"{USISDKClient.BaseAddress}/api/v1/SessionProposalEvaluationCriteriaResponses/{aintSessionProposalEvaluationCriteriaResponseId}").ConfigureAwait(false);
      APIUtil.SuccessResponse(response);
    }


    public static UngerboeckSDKPackage.PublicLanguagesModel GetPublicLanguage(HttpClient USISDKClient, int aintSequence)
    {
      Task<UngerboeckSDKPackage.PublicLanguagesModel> publicLanguageTask =
          GetAsync<UngerboeckSDKPackage.PublicLanguagesModel>(USISDKClient,
          $"PublicLanguages/{aintSequence}");
      return publicLanguageTask.Result;
    }

    public static UngerboeckSDKPackage.PublicLanguagesModel AddPublicLanguage(HttpClient USISDKClient, UngerboeckSDKPackage.PublicLanguagesModel publicLanguage)
    {
      Task<UngerboeckSDKPackage.PublicLanguagesModel> publicLanguageTask = PostAsync(USISDKClient, "PublicLanguages", publicLanguage);
      return publicLanguageTask.Result;
    }

    public static UngerboeckSDKPackage.PublicLanguagesModel UpdatePublicLanguage(HttpClient USISDKClient, UngerboeckSDKPackage.PublicLanguagesModel publicLanguage)
    {
      Task<UngerboeckSDKPackage.PublicLanguagesModel> publicLanguageTask = PutAsync(USISDKClient,
              $"PublicLanguages/{publicLanguage.Sequence}", publicLanguage);
      return publicLanguageTask.Result;
    }

    public static async Task AwaitDeletePublicLanguage(HttpClient USISDKClient, int aintSequence)
    {
      HttpResponseMessage response = await USISDKClient.DeleteAsync($"{USISDKClient.BaseAddress}/api/v1/PublicLanguages/{aintSequence}").ConfigureAwait(false);
      APIUtil.SuccessResponse(response);
    }

    public static UngerboeckSDKPackage.RegistrationPreferenceTypesModel GetRegistrationPreferenceType(HttpClient USISDKClient, int aintRegistrationPreferenceID)
    {
      Task<UngerboeckSDKPackage.RegistrationPreferenceTypesModel> registrationPreferenceTypeTask =
          GetAsync<UngerboeckSDKPackage.RegistrationPreferenceTypesModel>(USISDKClient,
          $"RegistrationPreferenceTypes/{aintRegistrationPreferenceID}");
      return registrationPreferenceTypeTask.Result;
    }

    public static UngerboeckSDKPackage.RegistrationPreferenceTypesModel AddRegistrationPreferenceType(HttpClient USISDKClient, UngerboeckSDKPackage.RegistrationPreferenceTypesModel registrationPreferenceType)
    {
      Task<UngerboeckSDKPackage.RegistrationPreferenceTypesModel> registrationPreferenceTypeTask = PostAsync(USISDKClient, "RegistrationPreferenceTypes", registrationPreferenceType);
      return registrationPreferenceTypeTask.Result;
    }

    public static UngerboeckSDKPackage.RegistrationPreferenceTypesModel UpdateRegistrationPreferenceType(HttpClient USISDKClient, UngerboeckSDKPackage.RegistrationPreferenceTypesModel registrationPreferenceType)
    {
      Task<UngerboeckSDKPackage.RegistrationPreferenceTypesModel> registrationPreferenceTypeTask = PutAsync(USISDKClient,
              $"RegistrationPreferenceTypes/{registrationPreferenceType.RegistrationPreferenceID}", registrationPreferenceType);
      return registrationPreferenceTypeTask.Result;
    }

    public static async Task AwaitDeleteRegistrationPreferenceType(HttpClient USISDKClient, int aintRegistrationPreferenceID)
    {
      HttpResponseMessage response = await USISDKClient.DeleteAsync($"{USISDKClient.BaseAddress}/api/v1/RegistrationPreferenceTypes/{aintRegistrationPreferenceID}").ConfigureAwait(false);
      APIUtil.SuccessResponse(response);
    }

    public static UngerboeckSDKPackage.RegistrationConfigurationsModel GetRegistrationConfiguration(HttpClient USISDKClient, string astrOrgCode, string astrConfigurationCode)
    {
      Task<UngerboeckSDKPackage.RegistrationConfigurationsModel> registrationConfigurationTask =
          GetAsync<UngerboeckSDKPackage.RegistrationConfigurationsModel>(USISDKClient,
          $"RegistrationConfigurations/{astrOrgCode}/{astrConfigurationCode}");
      return registrationConfigurationTask.Result;
    }

    public static UngerboeckSDKPackage.RegistrationConfigurationsModel AddRegistrationConfiguration(HttpClient USISDKClient, UngerboeckSDKPackage.RegistrationConfigurationsModel registrationConfig)
    {
      Task<UngerboeckSDKPackage.RegistrationConfigurationsModel> registrationConfigurationTask = PostAsync(USISDKClient, "RegistrationConfigurations", registrationConfig);
      return registrationConfigurationTask.Result;
    }

    public static UngerboeckSDKPackage.RegistrationConfigurationsModel UpdateRegistrationConfiguration(HttpClient USISDKClient, UngerboeckSDKPackage.RegistrationConfigurationsModel registrationConfig)
    {
      Task<UngerboeckSDKPackage.RegistrationConfigurationsModel> registrationConfigurationTask = PutAsync(USISDKClient,
              $"RegistrationConfigurations/{registrationConfig.OrganizationCode}/{registrationConfig.ConfigurationCode}", registrationConfig);
      return registrationConfigurationTask.Result;
    }

    public static async Task AwaitDeleteRegistrationConfiguration(HttpClient USISDKClient, string astrOrgCode, string astrConfigurationCode)
    {
      HttpResponseMessage response = await USISDKClient.DeleteAsync($"{USISDKClient.BaseAddress}/api/v1/RegistrationConfigurations/{astrOrgCode}/{astrConfigurationCode}").ConfigureAwait(false);
      APIUtil.SuccessResponse(response);
    }

    /// <summary>
    /// Gets the report parameters for the report being run.
    /// </summary>
    /// <param name="USISDKClient">The SDK Client</param>
    /// <param name="astrOrgCode">Org Code to run the report against</param>
    /// <param name="aintSequenceNumber">Report Master Sequence</param>
    /// <returns></returns>
    public static ParametersModel GetReportParameters(HttpClient USISDKClient, string astrOrgCode, int aintSequenceNumber)
    {
      Task<ParametersModel> reportsTask = GetAsync<ParametersModel>(USISDKClient, $"Reports/{astrOrgCode}/{aintSequenceNumber}/GetParameters");
      return reportsTask.Result;
    }

    /// <summary>
    /// Runs the report with the request passed in
    /// </summary>
    /// <param name="USISDKClient">The SDK Client</param>
    /// <param name="astrOrgCode">Org Code to run the report against</param>
    /// <param name="aintSequenceNumber">Report Master Sequence</param>
    /// <param name="reportRequestModel">Request Object with Settings and Parameters</param>
    /// <returns></returns>
    public static ReportResponseModel RunReport(HttpClient USISDKClient, string astrOrgCode, int aintSequenceNumber, ReportRequestModel reportRequestModel)
    {
      Task<ReportResponseModel> ReportRequestModelTask = PutAsyncBulk<ReportResponseModel, ReportRequestModel>(USISDKClient, $"Reports/{astrOrgCode}/{aintSequenceNumber}/RunReport", reportRequestModel);
      return ReportRequestModelTask.Result;
    }


    private static bool RequestFailed(HttpResponseMessage response)
    {
      //Something failed on the server side
      if (response.StatusCode == System.Net.HttpStatusCode.InternalServerError) return true;

      return false;
    }

    private static bool Unauthorized(HttpResponseMessage response)
    {
      //Most likely something wasn't included or was malformed in the header
      if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized) return true;

      return false;
    }

    private static bool NotFound(HttpResponseMessage response)
    {
      //Possibly bad data on retrieve
      if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return true;

      return false;
    }

    private static bool MethodNotAllowed(HttpResponseMessage response)
    {
      //Check to make sure the HTTP verb matches the function the request is hitting
      if (response.StatusCode == System.Net.HttpStatusCode.MethodNotAllowed) return true;

      return false;
    }

    private static bool SaveFailed(HttpResponseMessage response)
    {
      //All failed save attempts save with this status code
      if (response.StatusCode == System.Net.HttpStatusCode.BadRequest) return true;

      return false;
    }

    public static bool SuccessResponse(HttpResponseMessage response)
    {
      if (APIUtil.NotFound(response) || APIUtil.MethodNotAllowed(response) || APIUtil.Unauthorized(response) || APIUtil.SaveFailed(response) || APIUtil.RequestFailed(response))
      {
        if (APIUtil.Unauthorized(response) && response.Headers.WwwAuthenticate.Count > 0)
        {
          throw new Exception(response.Headers.WwwAuthenticate.FirstOrDefault().ToString()); // Specific authentication errors come back as their own header
        }
        else
        {
          throw new Exception(response.ReasonPhrase); // Exception messages are passed back from the API via the response's reason phrase property
        }
      }

      return true;
    }

    public static string GetAPIErrorMessage(Exception ex)
    {
      //Depending on when the exception is thrown, it may change the exception type.
      // Errors created during an async / await call get wrapped as an AggregateException
      if (ex.GetType() == typeof(AggregateException))
      {
        return ((AggregateException)ex).InnerExceptions.FirstOrDefault().Message;
      }
      else
      {
        return ex.Message;
      }

    }

    ///<summary>
    ///Convert a byte array into a base 64 string in chunks based on a multiple of 3.  This is done to help prevent a system out of memory exception.
    ///</summary>
    ///<param name="aobjBytes"></param>
    ///<returns></returns>
    public static string GetEncodedStringForDocuments(byte[] aobjBytes)
    {
      const int intChunk = 6480;
      var sbEncodedBytes = new StringBuilder();

      if (aobjBytes != null && aobjBytes.Length > 0)
      {
        if (aobjBytes.Length <= intChunk)
        {
          sbEncodedBytes.Append(Convert.ToBase64String(aobjBytes));
        }
        else
        {
          for (int i = 0; i <= (aobjBytes.Length - 1); i = (i + intChunk))
          {
            sbEncodedBytes.Append(Convert.ToBase64String(Slice(aobjBytes, i, i + intChunk)));
          }

        }

      }

      return sbEncodedBytes.ToString();
    }

    ///<summary>
    ///Convert base 64 string to byte array in chunks based on a multiple of 3.  This is done to prevent a system out of memory exception.
    ///</summary>
    ///<param name="astrBytes"></param>
    ///<returns></returns>

    public static byte[] DecodeStringForDocuments(string astrBytes)
    {
      byte[] objBytes = null;
      byte[] objBuffer = new byte[6479];

      if (!string.IsNullOrEmpty(astrBytes))
      {
        using (System.IO.MemoryStream objInputMemStream = new System.IO.MemoryStream())
        {
          using (System.IO.StreamWriter objStreamWriter = new System.IO.StreamWriter(objInputMemStream))
          {
            objStreamWriter.Write(astrBytes);
            objStreamWriter.Flush();
            objInputMemStream.Position = 0;
            using (System.IO.MemoryStream objOutputMemStream = new System.IO.MemoryStream())
            {
              int intBytesRead = objInputMemStream.Read(objBuffer, 0, objBuffer.Length);
              while (intBytesRead > 0)
              {
                string strBase64Chunk = System.Text.Encoding.UTF8.GetString(objBuffer, 0, intBytesRead);
                byte[] objByteChunk = Convert.FromBase64String(strBase64Chunk);
                objOutputMemStream.Write(objByteChunk, 0, objByteChunk.Length);
                intBytesRead = objInputMemStream.Read(objBuffer, 0, objBuffer.Length);
              }

              objBytes = objOutputMemStream.ToArray();
            }
          }
        }
      }

      return objBytes;
    }

    private static byte[] Slice(byte[] source, int startIndex, int endIndex)
    {
      int length;
      byte[] slice = null;
      if (source != null && source.Length > 0 && startIndex >= 0 && endIndex > startIndex)
      {
        if (endIndex > (source.Length - 1))
        {
          length = (source.Length - startIndex);
        }
        else
        {
          length = (endIndex - startIndex);
        }

        if (length > 0)
        {
          slice = new byte[length - 1 + 1];
          for (int i = 0; i <= length - 1; i++)
          {
            slice[i] = source[i + startIndex];
          }
        }
      }

      return slice;
    }



    [Obsolete("Use GetSearchList instead")]
    public static IEnumerable<AllAccountsModel> GetAccountList(HttpClient USISDKClient, string oDataString, string orgCode)
    {
      //This method only persists for old versions of the SDK Wrapper without the generic GetSearchList
      Task<IEnumerable<AllAccountsModel>> accountTask = AwaitGetAccountList(USISDKClient, oDataString, orgCode);
      return accountTask.Result;
    }

    private static async Task<IEnumerable<AllAccountsModel>> AwaitGetAccountList(HttpClient USISDKClient, string oDataString, string orgCode)
    {
      HttpResponseMessage response = await USISDKClient.GetAsync($"{USISDKClient.BaseAddress}/api/v1/Accounts/{orgCode + "?search=" + oDataString}").ConfigureAwait(false);
      if (SuccessResponse(response))
      {
        var accountList = await response.Content.ReadAsAsync<IEnumerable<AllAccountsModel>>();
        return accountList;
      }

      return null;
    }

    [Obsolete("Use GetSearchList instead")]
    public static IEnumerable<UngerboeckSDKPackage.AccountAffiliationsModel> GetAccountAffiliationsList(HttpClient USISDKClient, string oDataString, string orgCode)
    {
      Task<IEnumerable<UngerboeckSDKPackage.AccountAffiliationsModel>> accountAffiliations = AwaitGetAccountAffiliationsList(USISDKClient, oDataString, orgCode);

      return accountAffiliations.Result;
    }

    private static async Task<IEnumerable<UngerboeckSDKPackage.AccountAffiliationsModel>> AwaitGetAccountAffiliationsList(HttpClient USISDKClient, string oDataString, string orgCode)
    {
      HttpResponseMessage response = await USISDKClient.GetAsync($"{USISDKClient.BaseAddress}/api/v1/AccountAffiliations/{orgCode}?search={oDataString}").ConfigureAwait(false);
      if (APIUtil.SuccessResponse(response))
      {
        var accountAffiliationsList = await response.Content.ReadAsAsync<IEnumerable<UngerboeckSDKPackage.AccountAffiliationsModel>>();
        return accountAffiliationsList;
      }

      return null;
    }

    private static void AddValidationOverride(HttpClient USISDKClient, int validationCode)
    {
      if (USISDKClient.DefaultRequestHeaders.Contains("X-ValidationOverrides")) USISDKClient.DefaultRequestHeaders.Remove("X-ValidationOverrides");
      USISDKClient.DefaultRequestHeaders.Add("X-ValidationOverrides", $"[{{\"Code\": {validationCode}}}]");
    }    
  }
}
