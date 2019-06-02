using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CM.JS {
#pragma warning disable CS0649

    /// <summary>
    /// String resources. All actual text is stored in a *.CSV format dictionary for easy translation
    /// by professional services, if the need arises.
    /// </summary>
    /// <remarks>
    /// Strings are named according to a few categories:
    /// TITLE_
    /// LABEL_
    /// HTML_
    /// 
    /// Only HTML_ is permitted to contain code and should be closely scrutinised prior to commit.
    /// 
    /// </remarks>
    partial class SR {

        public static readonly Dictionary<string, string> Langauges = new Dictionary<string, string> {
            { "EN-GB", "English (UK)" },
            { "EN-CA", "English (Canada)" },
            { "EN-AU", "English (Australia)" },
            { "EN-US", "English (US)" }
        };



        public static string CurrentLanguage = "EN-UK";

        public static string CHAR_DECIMAL = ".";
        public static string CHAR_THOUSAND_SEPERATOR = ",";

        /// <summary>
        /// Civil Money Provides
        /// • A universal basic income
        /// • Implicit taxation
        /// • Seeding based on regional productivity
        /// • Transparent transactions and accountability
        /// • A more civilised, cash-free society
        /// </summary>
        public static string HTML_CIVIL_MONEY_PROVIDES;

        /// <summary>Account Name</summary>
        public static string LABEL_ACCOUNT_NAME;

        /// <summary>We apologise that not all languages are available. If you are interested in helping out, please contact us.</summary>
        public static string LABEL_CHOOSE_YOUR_LANGUAGE;

        /// <summary>Go</summary>
        public static string LABEL_GO;

        /// <summary>Create my account</summary>
        public static string LABEL_CREATE_MY_ACCOUNT;

        /// <summary>Please Select</summary>
        public static string LABEL_PLEASE_SELECT;

        /// <summary>Re-enter the pass phrase</summary>
        public static string LABEL_REENTER_PASS_PHRASE;


        /// <summary>Find an account</summary>
        public static string LABEL_FIND_AN_ACCOUNT;
        /// <summary>Enter account name</summary>
        public static string LABEL_ENTER_ACCOUNT_NAME;

        /// <summary>Region</summary>
        public static string LABEL_REGION;

        /// <summary>Civil Money is not like traditional online banking systems...</summary>
        public static string HTML_REGISTER_INTRO;

        /// <summary>Secret pass phrase</summary>
        public static string LABEL_SECRET_PASS_PHRASE;

        /// <summary>Withheld RSA Private Key</summary>
        public static string LABEL_WITHHELD_PRIVATE_KEY;

        /// <summary>Select my RSA private key file</summary>
        public static string LABEL_SELECT_MY_KEY_FILE;

        /// <summary>Paste from text instead</summary>
        public static string LABEL_PASTE_FROM_TEXT_INSTEAD;

        /// <summary>A more civilised fiat alternative.</summary>
        public static string LABEL_CIVIL_MONEY_SUB_HEADING;

        /// <summary>Account age</summary>
        public static string LABEL_ACCOUNT_AGE;

        /// <summary>Income eligibility</summary>
        public static string LABEL_INCOME_ELIGIBILITY;

        /// <summary>Working</summary>
        public static string LABEL_INCOME_ELIGIBILITY_WORKING;

        /// <summary>Looking for Working</summary>
        public static string LABEL_INCOME_ELIGIBILITY_LOOKING_FOR_WORK;

        /// <summary>Health Problem</summary>
        public static string LABEL_INCOME_ELIGIBILITY_HEALTH_PROBLEM;

        /// <summary>Retired</summary>
        public static string LABEL_INCOME_ELIGIBILITY_RETIRED;

        /// <summary>Attributes</summary>
        public static string LABEL_ACCOUNT_ATTRIBUTES;

        /// <summary>Not set</summary>
        public static string LABEL_VALUE_NOT_SET;

        /// <summary>{0} day(s)</summary>
        public static string LABEL_DAYS_OLD;

        /// <summary>{0} year(s)</summary>
        public static string LABEL_YEARS_OLD;

        /// <summary>Request a payment</summary>
        public static string LABEL_REQUEST_A_PAYMENT;

        /// <summary>Acceptance logos</summary>
        public static string LABEL_ACCEPTANCE_LOGOS;

        /// <summary>Make a payment</summary>
        public static string LABEL_MAKE_A_PAYMENT;

        /// <summary>Edit account</summary>
        public static string LABEL_EDIT_ACCOUNT;

        /// <summary>Skills & Services</summary>
        public static string LABEL_SKILLS_AND_SERVICES;

        /// <summary>Loading, please wait...</summary>
        public static string LABEL_LOADING_PLEASE_WAIT;

        /// <summary>Good Standing</summary>
        public static string LABEL_REPUTATION_GOOD;

        /// <summary>Overspent</summary>
        public static string LABEL_REPUTATION_OVERSPENT;

        /// <summary>Bad</summary>
        public static string LABEL_REPUTATION_BAD;

        /// <summary>Please select your current region.</summary>
        public static string LABEL_PLEASE_SELECT_YOUR_REGION;

        /// <summary>Your re-entered pass phrase doesn't match.</summary>
        public static string LABEL_PASSWORD_REENTRY_MISMATCH;

        /// <summary>Please enter a secret pass phrase.</summary>
        public static string LABEL_PASSWORD_REQUIRED;

        /// <summary>Please enter an account name.</summary>
        public static string LABEL_ACCOUNT_NAME_REQUIRED;

        /// <summary>Cancel</summary>
        public static string LABEL_CANCEL;

        /// <summary>Retry</summary>
        public static string LABEL_RETRY;

        /// <summary>Please wait</summary>
        public static string LABEL_PLEASE_WAIT;

        /// <summary>Generating a new secret key</summary>
        public static string LABEL_STATUS_GENERATING_NEW_SECRET_KEY;

        /// <summary>Processing your pass phrase</summary>
        public static string LABEL_STATUS_PROCESSING_PASS_PHRASE;

        /// <summary>Encrypting your secret key</summary>
        public static string LABEL_STATUS_ENCRYPTING_SECRET_KEY;

        /// <summary>Signing your information</summary>
        public static string LABEL_STATUS_SIGNING_INFORMATION;

        /// <summary>We were unable to sign the information. This usually means an incorrect password was entered.</summary>
        public static string LABEL_STATUS_SIGNING_FAILED;

        /// <summary>Contacting computers on the network</summary>
        public static string LABEL_STATUS_CONTACTING_NETWORK;

        /// <summary>Account created successfully</summary>
        public static string LABEL_STATUS_ACCOUNT_CREATED_SUCCESFULLY;

        /// <summary>Account updated successfully</summary>
        public static string LABEL_STATUS_ACCOUNT_UPDATED_SUCCESSFULLY;

        /// <summary>A problem occurred</summary>
        public static string LABEL_STATUS_A_PROBLEM_OCCURRED;

        /// <summary>Error (click for details)</summary>
        public static string LABEL_STATUS_ERROR_CLICK_FOR_DETAILS;

        /// <summary>Checking account name</summary>
        public static string LABEL_STATUS_CHECKING_ACCOUNT_NAME;

        /// <summary>We can't find account '{0}'. If this is unexpected, please try again later.</summary>
        public static string LABEL_STATUS_ACCOUNT_NOT_FOUND;

        /// <summary>Connecting</summary>
        public static string LABEL_STATUS_CONNECTING;

        /// <summary>Corroborating</summary>
        public static string LABEL_STATUS_CORROBORATING;

        /// <summary>Committing data</summary>
        public static string LABEL_STATUS_COMITTING_DATA;

        /// <summary>OK</summary>
        public static string LABEL_STATUS_OK;

        /// <summary>Please enter your account name in the field above.</summary>
        public static string LABEL_PLEASE_ENTER_YOUR_ACCOUNT_NAME;

        /// <summary>Enter a new pass phrase</summary>
        public static string LABEL_ENTER_A_NEW_PASS_PHRASE;

        /// <summary>Signing key looks good!</summary>
        public static string LABEL_SIGNING_KEY_LOOKS_OK;

        /// <summary>The text entered does not seem to contain a private key text blob.</summary>
        public static string LABEL_INVALID_RSA_KEY_TEXT_BLOB;

        /// <summary>Use an offline private key instead of a pass phrase.</summary>
        public static string LABEL_USE_AN_OFFLINE_PRIVATE_KEY;

        /// <summary>Generate a new key</summary>
        public static string LABEL_GENERATE_A_NEW_KEY;

        /// <summary>Generating a new key, please wait</summary>
        public static string LABEL_STATUS_GENERATING_NEW_KEY;

        /// <summary>Your new RSA private key has been generated...</summary>
        public static string LABEL_STATUS_NEW_KEY_GENERATED_OK;

        /// <summary>Go to your account</summary>
        public static string LABEL_GO_TO_YOUR_ACCOUNT;

        /// <summary>Go to {0}</summary>
        public static string LABEL_GO_TO_ACCOUNT_BLANK;

        /// <summary>Account name '{0}' is already taken.</summary>
        public static string LABEL_ACCOUNT_BLANK_IS_ALREADY_TAKEN;

        /// <summary>Account name '{0}' looks OK!</summary>
        public static string LABEL_ACCOUNT_BLANK_LOOKS_OK;

        /// <summary>A valid payee account name is required.</summary>
        public static string LABEL_A_VALID_PAYEE_ACCOUNT_NAME_IS_REQUIRED;

        /// <summary>Your account name is required.</summary>
        public static string LABEL_YOUR_ACCOUNT_NAME_IS_REQUIRED;

        /// <summary>Your account name</summary>
        public static string LABEL_YOUR_ACCOUNT_NAME;

        /// <summary>The amount is invalid.</summary>
        public static string LABEL_THE_AMOUNT_IS_INVALID;

        /// <summary>Unfortunately we can't reach a server right now.</summary>
        public static string LABEL_STATUS_PROBLEM_REACHING_A_SERVER;

        /// <summary>You'll need to re-enter this any time you do anything. Use multiple words to make up a memorable phrase. Complixity is not as important as overall length - an all lowercase sentence with spaces is fine.</summary>
        public static string LABEL_NEW_PASSWORD_INSTRUCTIONS;

        /// <summary>Account names contain only letters, numbers periods and dashes and must be at least three characters in length.</summary>
        public static string LABEL_ACCOUNT_NAME_INSTRUCTIONS;

        /// <summary>This is roughly equal to USD${0} or {1} hour(s) of somebody's time.</summary>
        public static string LABEL_AMOUNT_HINT;

        /// <summary>You will have a //c {0} balance and {1} reputation after payment.</summary>
        public static string LABEL_REMAINING_BALANCE_HINT;

        /// <summary>Sent transaction</summary>
        public static string LABEL_ALERT_SENT_TRANSACTION;

        /// <summary>Refunded transaction</summary>
        public static string LABEL_ALERT_REFUNDED_TRANSACTION;

        /// <summary>Cancelled transaction</summary>
        public static string LABEL_ALERT_CANCELLED_TRANSACTION;

        /// <summary>Declined transaction</summary>
        public static string LABEL_ALERT_DECLINED_TRANSACTION;

        /// <summary>Accepted transaction</summary>
        public static string LABEL_ALERT_ACCEPTED_TRANSACTION;

        /// <summary>Disputed transaction</summary>
        public static string LABEL_ALERT_DISPUTED_TRANSACTION;

        /// <summary>Account {0} has been modified</summary>
        public static string LABEL_ALERT_ACCOUNT_BLANK_MODIFIED;

        /// <summary>Clear</summary>
        public static string LABEL_CLEAR;

        /// <summary>Please pay</summary>
        public static string TITLE_PLEASE_PAY;

        /// <summary>Matching transaction received from</summary>
        public static string LABEL_MATCHING_TRANSACTION_RECEIVED_FROM;

        /// <summary>Point of Sale</summary>
        public static string LABEL_POINT_OF_SALE;

        /// <summary>Confirmation(s) (as in '5 confirmation(s)')</summary>
        public static string LABEL_CONFIRMATIONS;

        /// <summary>Notification(s) (as in '5 Notification(s)')</summary>
        public static string LABEL_NOTIFICATIONS;

        /// <summary>Dismiss All</summary>
        public static string LABEL_DISMISS_ALL;

        /// <summary>Refunded</summary>
        public static string LABEL_PAYEE_STATUS_REFUND;

        /// <summary>Pending</summary>
        public static string LABEL_PAYEE_STATUS_NOTSET;

        /// <summary>Accepted</summary>
        public static string LABEL_PAYEE_STATUS_ACCEPT;

        /// <summary>Declined</summary>
        public static string LABEL_PAYEE_STATUS_DECLINE;

        /// <summary>Accepted</summary>
        public static string LABEL_PAYER_STATUS_ACCEPT;

        /// <summary>Pending</summary>
        public static string LABEL_PAYER_STATUS_NOTSET;

        /// <summary>Disputed</summary>
        public static string LABEL_PAYER_STATUS_DISPUTE;

        /// <summary>Cancelled</summary>
        public static string LABEL_PAYER_STATUS_CANCEL;

        /// <summary>Refund</summary>
        public static string LABEL_PAYEE_STATUS_REFUND_VERB;

        /// <summary>Accept</summary>
        public static string LABEL_PAYEE_STATUS_ACCEPT_VERB;

        /// <summary>Decline</summary>
        public static string LABEL_PAYEE_STATUS_DECLINE_VERB;

        /// <summary>Accept</summary>
        public static string LABEL_PAYER_STATUS_ACCEPT_VERB;

        /// <summary>Dispute</summary>
        public static string LABEL_PAYER_STATUS_DISPUTE_VERB;

        /// <summary>Cancel</summary>
        public static string LABEL_PAYER_STATUS_CANCEL_VERB;

        /// <summary>
        /// Amateur
        /// </summary>
        public static string LABEL_SKILL_LEVEL_AMATEUR;

        /// <summary>
        /// Qualified
        /// </summary>
        public static string LABEL_SKILL_LEVEL_QUALIFIED;

        /// <summary>
        /// Experienced
        /// </summary>
        public static string LABEL_SKILL_LEVEL_EXPERIENCED;

        /// <summary>
        /// Certified
        /// </summary>
        public static string LABEL_SKILL_LEVEL_CERTIFIED;

        /// <summary>I promise to follow the Civil Money Honour Code</summary>
        public static string HTML_I_PROMISE_TO_FOLLOW_THE_HONOUR_CODE;

        /// <summary><b>I promise to try and not be an jerk.</b> If somebody else is being one to me, I will ignore them or <b>politely</b> remind them about the Civil Money Honour Code.</summary>
        public static string HTML_CIVIL_MONEY_HONOUR_CODE;

        /// <summary>
        /// If your web browser's address bar is not visible or its URL does not begin with        /// https://civil.money/ or if in the future you don't receive this reminder, please        /// do not type in your pass phrase, as the page you are on might be trying to
        /// steal your account.
        /// </summary>

        public static string LABEL_CIVIL_MONEY_SECURITY_REMINDER;

        /// <summary>
        /// I've checked my web browser's address and it definitely begins with <b>https://civil.money/</b>
        /// </summary>
        public static string HTML_IVE_CHECKED_MY_WEB_BROWSER_ADDRESS;

        /// <summary>Own this account?</summary>
        public static string TITLE_OWN_THIS_ACCOUNT;

        /// <summary>Transaction History</summary>
        public static string TITLE_TRANSACTION_HISTORY;

        /// <summary>Transaction Details</summary>
        public static string TITLE_TRANSACTION_DETAILS;

        /// <summary>Choose your language</summary>
        public static string TITLE_CHOOSE_YOUR_LANGUAGE;

        /// <summary>Civil Money</summary>
        public static string TITLE_CIVIL_MONEY;

        /// <summary>History</summary>
        public static string TITLE_HISTORY;

        /// <summary>Any accounts that have been viewed on this device are listed here for quick access.</summary>
        public static string LABEL_HISTORY_INTRO;

        /// <summary>There are no items in your viewing history.</summary>
        public static string LABEL_HISTORY_NO_ITEMS;

        /// <summary>Home</summary>
        public static string TITLE_HOMEPAGE;

        /// <summary>Help</summary>
        public static string TITLE_HELP;

        /// <summary>Peers</summary>
        public static string TITLE_PEERS;

        /// <summary>Regions</summary>
        public static string TITLE_REGIONS;

        /// <summary>Register</summary>
        public static string TITLE_REGISTER;

        /// <summary>Not Found</summary>
        public static string TITLE_NOT_FOUND;

        /// <summary>The link you have followed appears to be invalid.</summary>
        public static string LABEL_LINK_APPEARS_TO_BE_INVALID;

        /// <summary>If you're having trouble with the Civil Money service or have a question, please email us for assistance.</summary>
        public static string LABEL_HELP_INTRO;

        /// <summary>We regret that help is presently only available in English. If you would like to volunteer to help people in your native tongue please reach out.</summary>
        public static string LABEL_HELP_IN_ENGLISH_ONLY;

        /// <summary>Votes</summary>
        public static string TITLE_VOTING;

        /// <summary>
        /// When fundamental changes to the monetary system become necessary,         /// everybody has an opportunity to vote for or against those changes.
        /// </summary>
        public static string HTML_VOTES_INTRO;

        /// <summary>There are currently no propositions to display.</summary>
        public static string LABEL_VOTES_NO_PROPOSITIONS;

        /// <summary>
        /// Voting close date
        /// </summary>
        public static string LABEL_VOTING_CLOSE_DATE;

        /// <summary>
        /// Eligible participants
        /// </summary>
        public static string LABEL_VOTING_ELIGIBLE_PARTICIPANTS;

        /// <summary>
        /// Ineligible or unverified participants
        /// </summary>
        public static string LABEL_VOTING_INELIGIBLE_UNVERIFIED_PARTICIPANTS;

        /// <summary>For</summary>
        public static string LABEL_VOTE_FOR;

        /// <summary>Against</summary>
        public static string LABEL_VOTE_AGAINST;

        /// <summary>Ineligible</summary>
        public static string LABEL_VOTE_INELIGIBLE;

        /// <summary>Learn more or vote</summary>
        public static string LABEL_LEARN_MORE_OR_VOTE;

        /// <summary>Download data</summary>
        public static string LABEL_DOWNLOAD_DATA;

        /// <summary>Current Propositions</summary>
        public static string TITLE_CURRENT_PROPOSITIONS;

        /// <summary>Closed Propositions</summary>
        public static string TITLE_CLOSED_PROPOSITIONS;

        /// <summary>Proposition #{0}</summary>
        public static string TITLE_PROPOSITION_NUMBER;

        /// <summary>Known Negative Impacts</summary>
        public static string TITLE_KNOWN_NEGATIVE_IMPACTS;

        /// <summary>Known Positive Impacts</summary>
        public static string TITLE_KNOWN_POSITIVE_IMPACTS;

        /// <summary>My account</summary>
        public static string LABEL_MY_ACCOUNT;

        /// <summary>My vote</summary>
        public static string LABEL_MY_VOTE;

        /// <summary>Your vote selection is required.</summary>
        public static string LABEL_YOUR_VOTE_SELECTION_IS_REQUIRED;

        /// <summary>Your vote has been stored successfully :)</summary>
        public static string LABEL_VOTE_SUBMITTED_SUCCESSFULLY;

        /// <summary>Your last vote '{0}' was on {1}.</summary>
        public static string LABEL_YOUR_LAST_VOTE_OF_BLANK_WAS_ON_BLANK;

        /// <summary>You are not presently eligible for voting, however you may submit an ineligible vote for testing.</summary>
        public static string LABEL_YOU_ARE_NOT_PRESENTLY_ELIGIBLE_FOR_VOTING;

        /// <summary>Get Involved</summary>
        public static string TITLE_GET_INVOLVED;

        /// <summary>
        /// Civil Money is built and maintained by unpaid volunteers. The intention         /// is to establish a steering group comprised of experts from all corners of        /// the world in applicable fields -- network and software security, finance,
        /// business development, law and politics.If you're passionate about         /// rebuilding the world's monetary system for a better society, please         /// reach out.
        /// </summary>
        public static string LABEL_GET_INVOLVED_INTRO;

        /// <summary>Install a Server</summary>
        public static string TITLE_INSTALL_A_SERVER;


        /// <summary>Download</summary>
        public static string LABEL_DOWNLOAD;

        /// <summary>Source Code</summary>
        public static string TITLE_SOURCE_CODE;

        /// <summary>
        /// The Civil Money system is completely open source. Access to
        /// the repository will be made a available after our initial pilot phase.
        /// </summary>
        public static string LABEL_SOURCE_CODE_INTRO;

        /// <summary>Learn more</summary>
        public static string LABEL_LEARN_MORE;

        /// <summary>or (as in do X or Y.)</summary>
        public static string LABEL_OR;

        /// <summary>Balance</summary>
        public static string LABEL_BALANCE;

        /// <summary>Don't have an account?</summary>
        public static string LABEL_DONT_HAVE_AN_ACCOUNT;

        /// <summary>About Civil Money</summary>
        public static string TITLE_ABOUT;

        /// <summary>
        /// Civil Money is a debt-free monetary system.
        /// etc..
        /// </summary>
        public static string HTML_ABOUT;

        /// <summary>
        /// Account Settings
        /// </summary>
        public static string TITLE_ACCOUNT_SETTINGS;

        /// <summary>
        /// Account settings are designed to show minimal personally identifying information        /// whilst still providing some sort of context about your role in the community.
        /// </summary>
        public static string LABEL_ACCOUNT_SETTINGS_INTRO;

        /// <summary>
        /// There is no reason to lie here...
        /// </summary>
        public static string LABEL_INCOME_ELIGIBILITY_INTRO;

        /// <summary>
        /// List your skills, services or anything you can do...
        /// </summary>
        public static string LABEL_SKILLS_AND_SERVICES_INTRO;

        /// <summary>
        /// Add another
        /// </summary>
        public static string LABEL_ADD_ANOTHER_ITEM;

        /// <summary>
        /// Push Notifications
        /// </summary>
        public static string LABEL_PUSH_NOTIFICATIONS;

        /// <summary>
        /// Specify one or more HTTP end-points...
        /// </summary>
        public static string LABEL_PUSH_NOTIFICATIONS_INTRO;

        /// <summary>
        /// Change my secret pass phrase
        /// </summary>
        public static string LABEL_CHANGE_MY_PASS_PHRASE;

        /// <summary>
        /// Change my private key
        /// </summary>
        public static string LABEL_CHANGE_MY_PRIVATE_KEY;

        /// <summary>
        /// Continue
        /// </summary>
        public static string LABEL_CONTINUE;

        /// <summary>
        /// Skill or server
        /// </summary>
        public static string LABEL_ENTER_SKILL_OR_SERVICE;

        /// <summary>
        /// Label
        /// </summary>
        public static string LABEL_LABEL;

        /// <summary>
        /// Security
        /// </summary>
        public static string LABEL_SECURITY;

        /// <summary>
        /// There were no items returned by the network...
        /// </summary>
        public static string LABEL_NO_ITEMS_FOUND;

        /// <summary>
        /// Civil Money Regions
        /// </summary>
        public static string TITLE_CIVIL_MONEY_REGIONS;

        /// <summary>
        /// Instead of traditional taxation, geographical regions generate...
        /// </summary>
        public static string LABEL_REGIONS_INTRO;

        /// <summary>
        /// Instead of traditional taxation, geographical regions generate...
        /// </summary>
        public static string TITLE_BROWSE_REGIONS;

        /// <summary>
        /// Recent revenue
        /// </summary>
        public static string LABEL_RECENT_REVENUE;

        /// <summary>
        /// Revenue reports are updated periodically...
        /// </summary>
        public static string LABEL_REVENUE_REPORT_HINT;

        /// <summary>
        /// Last updated
        /// </summary>
        public static string LABEL_TIME_LAST_UPDATED;

        /// <summary>
        /// Pay To
        /// </summary>
        public static string LABEL_PAY_TO;

        /// <summary>
        /// From
        /// </summary>
        public static string LABEL_PAY_FROM;

        /// <summary>
        /// Memo
        /// </summary>
        public static string LABEL_MEMO;

        /// <summary>
        /// Amount
        /// </summary>
        public static string LABEL_AMOUNT;

        /// <summary>
        /// Optional
        /// </summary>
        public static string LABEL_OPTIONAL;

        /// <summary>
        /// Tag/Order #
        /// </summary>
        public static string LABEL_TAG;

        /// <summary>
        /// Transaction created successfully.
        /// </summary>
        public static string LABEL_STATUS_TRANSACTION_CREATED_SUCCESSFULLY;

        /// <summary>
        /// Transaction updated successfully.
        /// </summary>
        public static string LABEL_STATUS_TRANSACTION_UPDATED_SUCCESSFULLY;

        /// <summary>
        /// None of the transactions could be updated.
        /// </summary>
        public static string LABEL_STATUS_NO_TRANSACTIONS_UPDATED;

        /// <summary>
        /// All transaction were updated successfully
        /// </summary>
        public static string LABEL_STATUS_ALL_TRANSACTIONS_UPDATED;

        /// <summary>
        /// Some of the transactions could not be updated.
        /// </summary>
        public static string LABEL_STATUS_SOME_TRANSACTIONS_FAILED;

        /// <summary>
        /// Link for payment to {0}
        /// </summary>
        public static string LABEL_LINK_FOR_PAYMENT_TO;

        /// <summary>
        /// Read only
        /// </summary>
        public static string LABEL_READONLY;

        /// <summary>Preview</summary>
        public static string LABEL_PREVIEW;

        /// <summary>The Civil Money Honour Code</summary>
        public static string TITLE_THE_CIVIL_MONEY_HONOUR_CODE;



        /*

        /// <summary>Imagine if you didn't have to work</summary>
        public static string HTML_ABOUT_1_1;

        /// <summary>What would you do?</summary>
        public static string HTML_ABOUT_1_2;

        /// <summary>Today we work because we need money..</summary>
        public static string HTML_ABOUT_2_1;

        /// <summary>But why does it have to be this way?</summary>
        public static string HTML_ABOUT_2_2;

        /// <summary>What if instead, everybody..</summary>
        public static string HTML_ABOUT_3_1;

        /// <summary>Today's monetary system..</summary>
        public static string HTML_ABOUT_3_2;

        /// <summary>Every person whether retired..</summary>
        public static string HTML_ABOUT_4_1;

        /// <summary>Roughly equivalent to ..</summary>
        public static string HTML_ABOUT_4_2;

        /// <summary>There are no more banks...</summary>
        public static string HTML_ABOUT_5_1;

        /// <summary>Every user of Civil Money is a money lender..</summary>
        public static string HTML_ABOUT_5_2;

        /// <summary>There is no such thing as physical cash</summary>
        public static string HTML_ABOUT_6_1;

        /// <summary>There are only publicly visible and verifiable balances and credit scores...</summary>
        public static string HTML_ABOUT_6_2;

        /// <summary>The value of one Civil Money..</summary>
        public static string HTML_ABOUT_7_1;

        /// <summary>The mathematical constant of time..</summary>
        public static string HTML_ABOUT_7_2;

        /// <summary>Payments begin to depreciate...</summary>
        public static string HTML_ABOUT_8_1;

        /// <summary>This means it is impossible for a minority to hoard cash..</summary>
        public static string HTML_ABOUT_8_2;

        /// <summary>Because payments depreciate..</summary>
        public static string HTML_ABOUT_9_1;

        /// <summary>Depreciation of account debits along with..</summary>
        public static string HTML_ABOUT_9_2;

        /// <summary>Inverse-taxation generates revenue..</summary>
        public static string HTML_ABOUT_10_1;

        /// <summary>Tax evasion is impossible..</summary>
        public static string HTML_ABOUT_10_2;

        /// <summary>All account and transaction is public..</summary>
        public static string HTML_ABOUT_11_1;

        /// <summary>Data is stored on random untrusted...</summary>
        public static string HTML_ABOUT_11_2;

        /// <summary>Every person can vote..</summary>
        public static string HTML_ABOUT_12_1;

        /// <summary>People can digitally sign votes...</summary>
        public static string HTML_ABOUT_12_2;

        /// <summary>'Double spending' is allowed..</summary>
        public static string HTML_ABOUT_13_1;

        /// <summary>In the event of a dispute..</summary>
        public static string HTML_ABOUT_13_2;

        /// <summary>Implications</summary>
        public static string HTML_ABOUT_14_1;

        /// <summary>Potential Negative..</summary>
        public static string HTML_ABOUT_14_2;

        /// <summary>All you need is temporary access..</summary>
        public static string HTML_ABOUT_15_1;

        */

        public static void Load(string lang, Action onReady) {
            var dic = typeof(SR);
            // Always load the reference language (English UK)
            Populate(SR.EN_GB);
            if (lang != "EN-GB") {
                var key = lang.Replace("-", "_").ToUpper();
                var csv = dic[key] as string;
                if (csv != null)
                    Populate(csv);
            }
            CurrentLanguage = lang;
            onReady();
        }

        private static void Populate(string csv) {
            int cursor = 0;
            string[] line;
            var dic = typeof(SR);
            while ((line = csv.NextCsvLine(ref cursor)) != null) {
                if (line.Length > 1)
                    dic[line[0]] = line[1];
            }
            // Validation
            var keys = Type.GetOwnPropertyNames(dic);
            var missing = "";
            foreach (string key in keys) {
                if (key.IndexOf('_') == -1) continue;
                if (dic[key] == null) {
                    missing += key + " ";
                } else if ((key.StartsWith("LABEL_", StringComparison.OrdinalIgnoreCase)
                    || key.StartsWith("TITLE_", StringComparison.OrdinalIgnoreCase))
                    && dic[key].ToString().IndexOf('<') > -1) {
                    throw new Exception("BUG! DISALLOWED HTML on Key " + key);
                }
            }
            if (missing.Length > 0)
                throw new Exception("BUG! MISSING LANGUAGE KEYS: " + missing);
        }
    }
}
