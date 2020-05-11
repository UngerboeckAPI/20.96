﻿using System.Net.Http;
using UngerboeckSDKWrapper;
using UngerboeckSDKPackage;
using System.Collections.Generic;

namespace Examples.Operations
{
  public class Opportunities : Base
  {
    public Opportunities(HttpClient USISDKClient) : base(USISDKClient)
    {
    }

    /// <summary>
    /// A basic retrieve example
    /// </summary>
    public OpportunitiesModel Get(string orgCode, string accountCode, int sequenceNumber)
    {
      return APIUtil.GetOpportunity(USISDKClient, orgCode, accountCode, sequenceNumber);
    }

    /// <summary>
    /// How to retrieve all.  For high volume, we recommend using a specific query when searching, shown in the General class.
    /// </summary>   
    public IEnumerable<OpportunitiesModel> RetrieveAll(string orgCode)
    {
      SearchMetadataModel searchMetadata = null;
      return APIUtil.GetSearchList<OpportunitiesModel>(USISDKClient, ref searchMetadata, orgCode, "All");
    }


    /// <summary>
    /// Examples showing how to search using UserFields
    /// </summary>
    /// <param name="orgCode">Organization Code where the search will take place.  User fields are organization-based.</param>
    /// <returns></returns>
    public IEnumerable<OpportunitiesModel> SearchForUserFields(string orgCode)
    {
      SearchMetadataModel searchMetadata = null;

      //For non-account user fields, the format is [User field Class]|[User field Type]|[User field property name]"
      //This will only work for active User Fields in your organization.
      //Note for multi-value UDFs, it will convert to a CONTAINS search.

      //This is searching for Opportunity user fields of Issue Class = C (event sales), Issue Type code = 13, organization code = 10, and User Text 09 (TXT_09).  It will return opportunities where the value is 'N'      
      return APIUtil.GetSearchList<OpportunitiesModel>(USISDKClient, ref searchMetadata, orgCode, "C|13|10|UserText09 eq 'N'");
    }

    /// <summary>
    /// A basic add example
    /// </summary>
    /// <param name="orgCode"></param>
    /// <param name="description"></param>
    /// <param name="accountCode"></param>
    /// <param name="issueClass">Set this to the designation of the opportunity.  You can use USISDKConstants.AccountDesignations to help you.  Example value is UngerboeckSDKPackage.USISDKConstants.AccountDesignations.EventSales</param>
    /// <param name="issueType">Set the opportunity type using the type code.  Which User Defined Fields you use is dependent on this.</param>
    /// <param name="status">This should be set to a code value found in the opportunity statuses window.  Example value is "A"</param>    
    /// <param name="userNumber03Value">In this example, we will set User Number 03, but you can fill any user field on your Issue Type.</param>
    public OpportunitiesModel Add(string orgCode, string description, string accountCode, string issueClass, string issueType, string status, int userNumber03Value)
    {
      var myOpportunity = new OpportunitiesModel
      {
        Organization = orgCode,
        Description = description,
        Account = accountCode,
        Status = status, 
        Class = issueClass,
        Type = issueType,
        UserNumber03 = userNumber03Value

        //Contact = "00111111"  'Set this to the account code of the opportunity contact if you wish for it to attach to that contact
        //Salesperson = "ALB" 'Enter in the account code of the salesperson if you wish to set this
      };

      return APIUtil.AddOpportunity(USISDKClient, myOpportunity);
    }

    /// <summary>
    /// A basic edit example
    /// </summary> 
    public OpportunitiesModel Edit(string orgCode, string accountCode, int sequenceNumber, string NewText)
    {
      var myOpportunity = APIUtil.GetOpportunity(USISDKClient, orgCode, accountCode, sequenceNumber);

      myOpportunity.Description = NewText;

      return APIUtil.UpdateOpportunity(USISDKClient, myOpportunity);
    }

    /// <summary>
    /// A delete example
    /// </summary>  
    public void Delete(string orgCode, string accountCode, int sequenceNumber)
    {
      APIUtil.AwaitDeleteOpportunity(USISDKClient, orgCode, accountCode, sequenceNumber).Wait();
    }


  }
}
