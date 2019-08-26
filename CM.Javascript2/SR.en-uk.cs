#region License

//
// Civil Money is free and unencumbered software released into the public domain (unlicense.org), unless otherwise
// denoted in the source file.
//

#endregion License

namespace CM.JS {
#pragma warning disable CS0649
    partial class SR {

        /// <summary>
        /// The default English dictionary.
        /// </summary>
        private static readonly string EN_GB = @"
KEY,REFERENCE,NATIVE
CHAR_DECIMAL,.,
CHAR_THOUSAND_SEPERATOR,"","",
TITLE_CIVIL_MONEY,Civil Money,Civil Money
LABEL_CIVIL_MONEY_SUB_HEADING,A more civilised monetary system for humanity.,
LABEL_ACCOUNT_NAME,Account Name,
LABEL_GO,Go,
HTML_CIVIL_MONEY_PROVIDES,""<h3>Civil Money is free to use and aims to provide everybody:</h3>
<ul>
<li>A generous universal basic income</li>
<li>Easy dispute resolution</li>
<li>Automatic taxation/funding for your country and region</li>
<li>Open transactions, honesty and fairness</li>
<li>A more civilised debt-free society</li>
</ul>"",
LABEL_CREATE_MY_ACCOUNT,Establish an Account,
LABEL_FIND_AN_ACCOUNT,Find an account,
LABEL_ENTER_ACCOUNT_NAME,Enter account Name,
TITLE_PEERS,Peers,
TITLE_REGIONS,Regions,
TITLE_HISTORY,History,
TITLE_HOMEPAGE,Home,
TITLE_REGISTER,Register,
TITLE_HELP,Help,
TITLE_CHOOSE_YOUR_LANGUAGE,Choose your language,
LABEL_CHOOSE_YOUR_LANGUAGE,""We apologise that not all languages are available. If you are interested in helping out, please contact us."",
HTML_REGISTER_INTRO,""Civil Money is not like traditional websites. It does not want even so much as your e-mail address, let alone any of your personally identifying information.
<br/>
<br/>The authenticity and reputation of your account is established only between yourself and the people or businesses in real life that you do business with.
<br/>
<br/>Please choose an account name and a secret pass phrase below to get started.
"",
LABEL_HISTORY_INTRO,Any accounts that have been viewed on this device are listed here for quick access.,
LABEL_HISTORY_NO_ITEMS,There are no items in your viewing history.,
LABEL_SECRET_PASS_PHRASE,Secret pass phrase,
LABEL_REENTER_PASS_PHRASE,Re-enter the pass phrase,
LABEL_REGION,Region,
LABEL_PLEASE_SELECT,Please select,
LABEL_DAYS_OLD,{0} day(s) old,
LABEL_YEARS_OLD,{0} year(s) old,
LABEL_ACCOUNT_AGE,Account age,
LABEL_INCOME_ELIGIBILITY,Income eligibility,
LABEL_ACCOUNT_ATTRIBUTES,Attributes,
LABEL_INCOME_ELIGIBILITY_WORKING,Working,
LABEL_INCOME_ELIGIBILITY_LOOKING_FOR_WORK,Looking for work,
LABEL_INCOME_ELIGIBILITY_HEALTH_PROBLEM,Health Problem,
LABEL_INCOME_ELIGIBILITY_RETIRED,Retired,
LABEL_VALUE_NOT_SET,Not set,
TITLE_OWN_THIS_ACCOUNT,Own this account?,
LABEL_MAKE_A_PAYMENT,Make a payment,
LABEL_REQUEST_A_PAYMENT,Point of Sale / Generate link,
LABEL_EDIT_ACCOUNT,Edit account,
LABEL_ACCEPTANCE_LOGOS,Acceptance logos,
LABEL_SKILLS_AND_SERVICES,Skills & Services,
TITLE_TRANSACTION_HISTORY,Transaction History,
TITLE_TRANSACTION_DETAILS,Transaction Details,
LABEL_LOADING_PLEASE_WAIT,""Loading, please wait..."",
LABEL_REPUTATION_GOOD,Good Standing,
LABEL_REPUTATION_OVERSPENT,Overspent,
LABEL_REPUTATION_BAD,Bad,
LABEL_NEW_PASSWORD_INSTRUCTIONS,""Your pass phrase is never transmitted over the internet or stored on any servers. It is irrecoverable if forgotten. Please use multiple words to make up a unique but memorable sentence. Complexity isn't as important as overall length. An all lower-cased sentence with spaces is ideal, keeping in mind that computers can do millions of guesses per second. "",
HTML_I_PROMISE_TO_FOLLOW_THE_HONOUR_CODE, ""I promise to follow the <b>Civil Money Honour Code</b>"",
HTML_CIVIL_MONEY_HONOUR_CODE,""<ol>
<li>I will try my best to not be a jerk. If somebody is being one to me, I will either ignore them or politely remind them about the Civil Money Honour Code.</li>
<li>I will respect any person's decision to decline my payment, regardless of reason or for no reason at all.</li>
<li>I will hold in the highest regard any person doing an unpleasant job in exchange for Civil Money and endeavour to eliminate unpleasant jobs through the sharing of ideas, science and ingenuity.</li>
<li>I accept that Civil Money is backed only by society at large and holds no intrinsic value, and is also an imperfect system. As such, the higher the cost of a good or service, the closer I will scrutinise a person's credit rating and transaction history, just as banks do today for a loan.</li>
<li>I accept that the value of all Civil Money transactions begin to depreciate to zero after 12 months, which helps to stimulate the economy, aids in the prevention of inflation, over accumulation of money, and forgives people of their debts to society after 2 years.</li>
</ol>
"",
LABEL_PLEASE_SELECT_YOUR_REGION,Please select your current region.,
LABEL_PASSWORD_REENTRY_MISMATCH,Your re-entered pass phrase doesn't match.,
LABEL_PLEASE_WAIT,Please wait,
LABEL_STATUS_GENERATING_NEW_SECRET_KEY,Generating new secret key,
LABEL_STATUS_PROCESSING_PASS_PHRASE,Processing your pass phrase,
LABEL_STATUS_ENCRYPTING_SECRET_KEY,Encrypting your secret key,
LABEL_STATUS_SIGNING_INFORMATION,Signing your information,
LABEL_STATUS_CONTACTING_NETWORK,Contacting computers on the network,
LABEL_STATUS_ACCOUNT_CREATED_SUCCESFULLY,Account created successfully.,
LABEL_STATUS_A_PROBLEM_OCCURRED,A problem occurred,
LABEL_GO_TO_YOUR_ACCOUNT,Go to your account,
LABEL_GO_TO_ACCOUNT_BLANK,Go to {0},
LABEL_ACCOUNT_NAME_INSTRUCTIONS,""Account names contain only letters, numbers and must be at least three characters in length."",
LABEL_ACCOUNT_BLANK_IS_ALREADY_TAKEN,Account name '{0}' is already taken.,
LABEL_ACCOUNT_BLANK_LOOKS_OK,Account name '{0}' looks OK!,
LABEL_STATUS_CHECKING_ACCOUNT_NAME,Checking account name,
LABEL_STATUS_PROBLEM_REACHING_A_SERVER,Unfortunately we can't reach a server right now.,
LABEL_CIVIL_MONEY_SECURITY_REMINDER,""If your web browser address bar is not visible or its URL does not begin withhttps://civil.money/ or if in the future you don't receive this reminder, pleasedon't enter a pass phrase or private key, as the page you are on might be trying to steal your account."",
HTML_IVE_CHECKED_MY_WEB_BROWSER_ADDRESS,I've checked my web browser's address bar and it definitely begins with <b>https://civil.money/</b>,
LABEL_A_VALID_PAYEE_ACCOUNT_NAME_IS_REQUIRED,A valid payee account name is required.,
LABEL_YOUR_ACCOUNT_NAME_IS_REQUIRED,Your account name is required.,
LABEL_THE_AMOUNT_IS_INVALID,The amount is invalid.,
LABEL_STATUS_SIGNING_FAILED,We were unable to sign the information. This usually means an incorrect password was entered.,
LABEL_CANCEL,Cancel,
LABEL_AMOUNT_HINT,This is roughly equal to USD${0} or {1} hour(s) of somebody's time.,
LABEL_REMAINING_BALANCE_HINT,You will have a //c {0} balance and {1} reputation after payment.,
LABEL_STATUS_ACCOUNT_NOT_FOUND,""We can't find account '{0}'. If this is unexpected, please try again later."",
LABEL_RETRY,Retry,
LABEL_STATUS_CONNECTING,Connecting,
LABEL_STATUS_OK,OK,
LABEL_STATUS_CORROBORATING,Corroborating,
LABEL_STATUS_COMITTING_DATA,Committing data,
LABEL_PAYEE_STATUS_NOTSET,Pending,
LABEL_PAYEE_STATUS_REFUND,Refunded,
LABEL_PAYEE_STATUS_DECLINE,Declined,
LABEL_PAYEE_STATUS_ACCEPT,Accepted,

LABEL_PAYER_STATUS_NOTSET,Pending,
LABEL_PAYER_STATUS_ACCEPT,Accepted,
LABEL_PAYER_STATUS_DISPUTE,Disputed,
LABEL_PAYER_STATUS_CANCEL,Cancelled,

LABEL_PAYEE_STATUS_REFUND_VERB,Refund,
LABEL_PAYEE_STATUS_DECLINE_VERB,Decline,
LABEL_PAYEE_STATUS_ACCEPT_VERB,Accept,

LABEL_PAYER_STATUS_CANCEL_VERB,Cancel,
LABEL_PAYER_STATUS_ACCEPT_VERB,Accept,
LABEL_PAYER_STATUS_DISPUTE_VERB,Dispute,

LABEL_SKILL_LEVEL_AMATEUR,Amateur,
LABEL_SKILL_LEVEL_QUALIFIED,Qualified,
LABEL_SKILL_LEVEL_EXPERIENCED,Experienced,
LABEL_SKILL_LEVEL_CERTIFIED,Certified,
LABEL_STATUS_ACCOUNT_UPDATED_SUCCESSFULLY,Account updated successfully.,
LABEL_PASSWORD_REQUIRED,Please enter a secret pass phrase.,
LABEL_ACCOUNT_NAME_REQUIRED,Please enter an account name.,
TITLE_NOT_FOUND,Not Found,
LABEL_LINK_APPEARS_TO_BE_INVALID,The link you have followed appears to be invalid.,
LABEL_HELP_INTRO,""If you're having trouble with the Civil Money service or have a question, please email us for assistance."",
LABEL_HELP_IN_ENGLISH_ONLY,""We regret that help is presently only available in English. If you would like to volunteer to help people in your native tongue please reach out."",
TITLE_VOTING,Voting,
HTML_VOTES_INTRO,""When fundamental changes to the monetary system become necessary, everybody has an opportunity to vote for or against those changes.<br/><br/>
Propositions that are up for voting are listed below. There are a few things that we do to help minimise votes coming from accounts that were specifically created in order to influence an outcome. <ol><li>The minimum requirement for voting is a good standing and at least one settled transaction for every 30 day period, for the past year.</li><li>You can change your vote at any time, however votes created or updated after the closing date will be considered ineligible.</li><li>A two-thirds majority of the eligible votes is required for any proposition to pass, meaning a significant winning margin is needed before proposed changes are introduced.</li><li>Voting outcomes follow the scientific method and are not locked in stone until a consensus with a reasonably low margin of error has been established.</li><li>Researchers are encouraged to collect, analyse, validate and calculate results independently and report their findings to the Civil Money steering group.</li><li>Everybody is welcome to download the latest voting data to confirm whether or not their vote has been counted.</li></ol>"",
LABEL_VOTES_NO_PROPOSITIONS,""There are currently no propositions to display."",
LABEL_VOTING_CLOSE_DATE,Voting close date,
LABEL_VOTING_ELIGIBLE_PARTICIPANTS,Eligible participants,
LABEL_VOTING_INELIGIBLE_UNVERIFIED_PARTICIPANTS,Ineligible or unverified participants,
LABEL_VOTE_FOR,For,
LABEL_VOTE_AGAINST,Against,
LABEL_VOTE_INELIGIBLE,Ineligible,
LABEL_LEARN_MORE_OR_VOTE,Learn more or vote,
LABEL_DOWNLOAD_DATA,Download data,
LABEL_STATUS_ERROR_CLICK_FOR_DETAILS,Error (click for details),
LABEL_YOUR_LAST_VOTE_OF_BLANK_WAS_ON_BLANK,Your last vote '{0}' was on {1}.,
LABEL_YOU_ARE_NOT_PRESENTLY_ELIGIBLE_FOR_VOTING,""You are not presently eligible for voting, however you may submit an ineligible vote for testing purposes."",
TITLE_CURRENT_PROPOSITIONS,Current Propositions,
TITLE_CLOSED_PROPOSITIONS,Closed Propositions,
TITLE_PROPOSITION_NUMBER,Proposition #{0},
TITLE_KNOWN_NEGATIVE_IMPACTS,Known Negative Impacts,
TITLE_KNOWN_POSITIVE_IMPACTS,Known Positive Impacts,
LABEL_MY_ACCOUNT,My account,
LABEL_MY_VOTE,My vote
LABEL_YOUR_VOTE_SELECTION_IS_REQUIRED,Your vote selection is required.,
LABEL_VOTE_SUBMITTED_SUCCESSFULLY,Your vote has been stored successfully :),
TITLE_GET_INVOLVED,Get Involved,
LABEL_GET_INVOLVED_INTRO,""Civil Money is built and maintained by unpaid volunteers. The intention is to establish a steering group comprised of experts from all corners ofthe world in applicable fields -- network and software security, finance, business development, law and politics. If you're passionate about rebuilding the world's economy for a better society, please reach out."",
TITLE_INSTALL_A_SERVER,Install a Server,
LABEL_DOWNLOAD,Download,
TITLE_SOURCE_CODE,Source Code,
LABEL_SOURCE_CODE_INTRO,""Civil Money is free and unencumbered software released into the public domain. The reference implementation and API has been published on GitHub."",
TITLE_ABOUT,About Civil Money,

TITLE_ACCOUNT_SETTINGS,Account Settings,
LABEL_ACCOUNT_SETTINGS_INTRO,""Account settings are designed to show minimal personally identifying informationwhilst still providing some sort of context about your role in the community."",
LABEL_INCOME_ELIGIBILITY_INTRO,""There is no reason to lie here. Good sellers should accept your payment for essential items if your transaction history is reasonable regardless of balance. Health Problem and Retired status does not give you a free pass to overspend your basic income."",
LABEL_SKILLS_AND_SERVICES_INTRO,""List your skills, services or anything you can do within your community, even if only in an amateur capacity."",
LABEL_ADD_ANOTHER_ITEM,Add another,
LABEL_PUSH_NOTIFICATIONS,Push Notifications,
LABEL_PUSH_NOTIFICATIONS_INTRO,""Specify one or more HTTP end-points to receive push notifications
any time your account is changed or a transaction is updated."",
LABEL_CHANGE_MY_PASS_PHRASE,Change my secret pass phrase,
LABEL_CHANGE_MY_PRIVATE_KEY,Change my private key,
LABEL_CONTINUE,Continue,
LABEL_ENTER_SKILL_OR_SERVICE,Skill or service,
LABEL_LABEL,Label,
LABEL_SECURITY,Security,
LABEL_NO_ITEMS_FOUND,""There were no items returned by the network. If this is unexpected, please check again in a few hours time."",
TITLE_CIVIL_MONEY_REGIONS,Civil Money Regions,
LABEL_REGIONS_INTRO,""Instead of traditional taxation, geographical regions generate new money
based on productivity. You can think of it as an inverse-tax where instead of us subtracting your income
out of pocket, 10% of every transaction generates new money for your region, which is then used
to fund government services, infrastructure and all manner of other necessities for a modern civilised
lifestyle. This way money gets distributed fairly based on who is actually being most productive,
and it is impossible for individuals or corporations to evade tax."",
TITLE_BROWSE_REGIONS,Browse regions,
LABEL_RECENT_REVENUE,Recent revenue,
LABEL_REVENUE_REPORT_HINT,""Revenue reports are updated periodically by the authoritative Civil Money service."",
LABEL_TIME_LAST_UPDATED,Last updated,
LABEL_PAY_TO,Pay to,
LABEL_PAY_FROM,From,
LABEL_MEMO,Memo,
LABEL_AMOUNT,Amount,
LABEL_OPTIONAL,Optional,
LABEL_TAG,Tag/Label,
LABEL_STATUS_TRANSACTION_CREATED_SUCCESSFULLY,Transaction created successfully.,
LABEL_STATUS_TRANSACTION_UPDATED_SUCCESSFULLY,Transaction updated successfully.,
LABEL_STATUS_NO_TRANSACTIONS_UPDATED,None of the transactions could be updated.,
LABEL_STATUS_ALL_TRANSACTIONS_UPDATED,All transaction were updated successfully.,
LABEL_STATUS_SOME_TRANSACTIONS_FAILED,Some of the transactions could not be updated.,
LABEL_LINK_FOR_PAYMENT_TO,Generate link for payment to {0},
LABEL_READONLY,Read only,
LABEL_PREVIEW,Preview,
LABEL_OR,or,
LABEL_BALANCE,Balance,
LABEL_LEARN_MORE,Learn more,
LABEL_DONT_HAVE_AN_ACCOUNT,Don't have an account?,
LABEL_YOUR_ACCOUNT_NAME,Your account name,
LABEL_ALERT_SENT_TRANSACTION,Sent transaction,
LABEL_ALERT_REFUNDED_TRANSACTION,Refunded transaction,
LABEL_ALERT_CANCELLED_TRANSACTION,Cancelled transaction,
LABEL_ALERT_DECLINED_TRANSACTION,Declined transaction,
LABEL_ALERT_ACCEPTED_TRANSACTION,Accepted transaction,
LABEL_ALERT_DISPUTED_TRANSACTION,Disputed transaction,
LABEL_CONFIRMATIONS,Confirmation(s),
LABEL_NOTIFICATIONS,Notification(s),
LABEL_ALERT_ACCOUNT_BLANK_MODIFIED,Account {0} has been modified,
LABEL_DISMISS_ALL,Dismiss All,
LABEL_CLEAR,Clear,
TITLE_PLEASE_PAY,Please pay,
LABEL_WITHHELD_PRIVATE_KEY,Withheld private key,
LABEL_SELECT_MY_KEY_FILE,Select my RSA private key file,
LABEL_PASTE_FROM_TEXT_INSTEAD,Paste from text instead,
LABEL_ENTER_A_NEW_PASS_PHRASE,Enter a new pass phrase,
LABEL_MATCHING_TRANSACTION_RECEIVED_FROM,Matching transaction received from,
LABEL_POINT_OF_SALE,Point of Sale,
LABEL_PLEASE_ENTER_YOUR_ACCOUNT_NAME,Please enter your account name in the field above.,
LABEL_SIGNING_KEY_LOOKS_OK,Signing key looks good!,
LABEL_USE_AN_OFFLINE_PRIVATE_KEY,Use an offline private key instead of a pass phrase.,
LABEL_INVALID_RSA_KEY_TEXT_BLOB,The text entered does not seem to contain a private key text blob.,
LABEL_GENERATE_A_NEW_KEY,Generate a new key,
LABEL_STATUS_GENERATING_NEW_KEY,Generating a new key, please wait... this can take a minute on slower devices.,
LABEL_STATUS_NEW_KEY_GENERATED_OK,""Your new RSA private key has been generated. Copy & Paste this blob of text into a safe offline location as a *.key text file. You may then select your *.key text file for signing future transactions."",
TITLE_THE_CIVIL_MONEY_HONOUR_CODE,The Civil Money Honour Code,
LABEL_STATUS_GOVERNINGAUTHORITY_CHECK_FAILED, There was a problem validating the signature of this governing authority account.,
TITLE_GOVERNINGAUTHORITY_FOR_BLANK,Governing Authority for {0},
HTML_ABOUT,""<h1>A society built on a minted currency is toxic.</h1><p><b>People are working multiple jobs, neglecting their children, even killing one another, all because of nothing more than imaginary computer data sitting on bank servers that we like to call money.</b></p><p>For most of us, every waking hour revolves around doing <em>something</em> in order to undo a mortgage or credit database entry. Money that banks literally create out of thin air and inject into the economy, forcing the majority into a position of slavery for fear of repossession. </p><p>Civil Money is a <a href=""""https://github.com/civilmoney/Reference-Implementation/blob/master/API.md"""" target=""""_blank"""">protocol</a> designed for people who wish to no longer waste their lives working to the benefit of a minority. A global effort to rebuild communities, given the past 200 years of economic adolescence leading current civilisation astray. Here's how it works...</p><img src=/ubi.svg><h2>Every person whether retired, studying, disabled or working receives a generous basic income.</h2><p>Roughly equivalent to USD $60,000 /yr. The idea is that <b>if</b> you work, it is gravy. Work on something because you're actually passionate about it, not because you have to. Stay home and raise your kids properly. Go to school. Do something amazing. If the people and local businesses that you rely on for day-to-day living all chose to accept payment in Civil Money (//c) the decision would be up to you.</p>
<img src=/taxation.svg>
<h2>An automatic, inverted taxation system generates money for regions based on their actual contribution to humanity.</h2>
<p>Tax evasion is impossible, we don't subtract money out of pocket and there is never any tax filing. In other words - <b>tax is dead.</b></p>
<p>10% of every settled transaction is automatically generated and placed into an authoritative Civil Money account for the seller's geographical region. Any change to the inverse-taxation algorithm will not directly impact people's account balances. Inverse-taxation is a data analysis/computer sciences problem. Specifically, we want to exclude transactions for inverse-tax when a money trail or account looks like it might have been deliberately created to generate false revenue.</p>

<img src=/datadistribution.svg>
<h2>All account and transaction information is public and distributed around the world.</h2>
<p>Data is stored on random untrusted computers and authenticity is established through a simple consensus.</p>
<p>There is nothing novel or unique about Civil Money's technology.</p>
<p>Because all data is public, it cannot be used for crime. Not that crimes of desperation need exist in the first place given a generous basic income. Blatantly predatory or deceitful users should be declined, ignored and politely reminded of the Civil Money Honour Code.</p>

<img src=/nobanks.svg>
<h2>There are no more banks, foreign exchange rates or financial speculation markets.</h2>
<p>Every user of Civil Money is a money lender. You get paid no matter what by simply clicking 'accept' on any payment, but the question you should ask yourself is, <em>&quot;should I?&quot;</em></p>
<p><b>Everybody is a bank</b> under Civil Money, so the willingness to support an exciting sounding business venture, or the level of compassion toward a customer's unfortunate life circumstance is up to each individual business or seller. There is no incentive either way to decline a customer. </p>
<p>Traditionally a bank's role in society is to do nothing more than protect people from thieves and charlatans. Since Civil Money is a public ledger for verifying any participant's credit score or transaction history, a banking and investment sector is unnecessary.</p>

<img src=/valuetime.svg>
<h2>The value of one Civil Money is equal to at least one hour of a person's time, or 50 bucks.</h2>
<p>To transition away from the old economy, a fixed and reasonable exchange rate is set at <b>//c 1.00 = USD $50</b>. This is based on an upper-middle class USD$ 80,000/yr income over an 8hr work day, 200 days a year (excludes 165 days of weekends/personal/sick/vacation time.)</p>
<blockquote>USD$ 80,000 / 1600hrs = $50/hr.<br />
Since 1hr = //c 1.00 it follows that //c 1.00 = USD$50</blockquote>
<p>This means that a person making designer T-shirts in Bangladesh, which might take a few hours, can no longer be expected to sell their time for a pittance or be compelled to work for a slave wage. Provided that they have access to materials and a personal website, that person can now sell their shirts directly to anybody in the world for a fair value of //c 3.00, equivalent to USD$150, or about what a retail chain might charge in western countries today.</p>
<p>Additionally, 1hr = //c 1.00 is not a fixed rule but rather a generally acceptable minimum wage. For seasonal workers or people in high demand, there is no restriction on how much they can charge for their time.</p>

<img src=/disputes.svg>
<h2>Dispute resolution is built-in.</h2>
<p>In the event of a dispute, a customer can always get their money back whilst the seller keeps their payment as well. Arguments are always settled amicably by default.</p>
<p>Rampant use of the dispute system would lead to inflation, so <b>the catch is</b>, it reflects badly on anyone who abuses it, or any seller who routinely does not volunteer a refund during disputes. </p>
<p>This system enables access to a fair dispute resolution process to people in countries that currently have no reliable legal system in place, whilst simultaneously reducing the burden on small-claims courts in the countries that do.</p>

<img src=/demurrage.svg>
<h2>Payments start to depreciate after 12 months.</h2>
<p>This is difficult to grasp at first, but it benefits the majority in profound ways and is ultimately a non-issue once you've discarded your traditional understanding of money and savings.</p>
<p><b>You never need to save, there is no debt, and investment and interest do not exist because every individual is a bank.</b></p>
<p>After 12 months, every transaction slowly depreciates down to 0.00 over the following 12 months. So by 2 years a buyer is always forgiven of their debt and the seller's earnings are removed from the economy.</p>
<p>Apart from preventing inflation, it makes it impossible for a minority to hoard cash or to use it as a mechanism to enslave others. One of the flaws in the traditional monetary system is that if you have a bunch of money, you can make more of it by not actually doing anything yourself, but instead gambling it on an abstract investment market driven by little more than a fleeting consensus of value which is being egged on by a herd of onlookers. Under Civil Money everybody is encouraged to spend their money soon and in exchange for real tangible goods or services, which ensures the distribution of wealth and a happy and healthy global economy. </p>
<p>Accounts that ping-pong money in order to circumvent depreciation can be openly identified and declined. Capitalism is not prohibited, but your capital can no longer be held onto long-term in the form of money. Instead it must be converted into physical assets and property. You know... real stuff.</p>

<img src=/hardtimes.svg>
<h2>Because payments depreciate, your credit score automatically restores itself if you fall on hard times.</h2>
<p>Depreciation of account debits along with a perpetually replenishing basic income ensures that people retain their dignity in the event of financial disaster. Nobody needs to struggle without access to essential goods and services for an extended period of time, if at all.</p>

<h2>The barrier to entry is virtually non-existent, and personally identifying information is disallowed.</h2>
<p>Civil Money is designed to work just as effectively for a remote village in Africa sharing a single smartphone, as it does a person standing in a shopping mall at a point of sale terminal. All you ever need is <b>temporary</b> access to a reasonably up-to-date web browser to create an account or complete a transaction.</p>
<p>Because all data is public and poorly implemented business processes sometimes use bills or receipt numbers as a proof of identity, we do not permit storage of anything that traditionally has been used as a source of ID. Civil Money does not allow storage of even so much as your e-mail address.</p>

<img src=/voting.svg>
<h2>Every person can vote on changes to the system.</h2>
<p>People can digitally sign votes in the same manner as regular transactions. A two-thirds majority is needed for any proposition to pass, meaning a significant winning margin is required before any changes are introduced. No vote counts more than another, however the minimum requirement for voting is a good standing and at least one settled transaction for every 30 day period, for the past year. This is simply to deter casual vote stuffing.</p>
<p>Because Civil Money is a completely transparent system, researchers are encouraged to collect, validate and calculate results independently and report their findings. Voting outcomes are a scientific process and not locked in stone until a consensus with a reasonably low margin of error is established.</p>


<h2>Identity verification is not a feature of Civil Money.</h2>
<p><b>The value and authenticity of your account exists only through pre-established relationships with people and companies that you choose to associate with.</b></p>
<img src=/ripmoney.svg>
<p>Any idiot can make a bunch of fake accounts and send themselves money -- however it's a worthless pursuit and they're just wasting their time. The Civil Money design is such that illegitimate transactions and accounts are deemed worthless by the community at large, who may easily run a credit report in order to trace a corroborated money trail and determine a customer's legitimacy.</p>
<p>Let's provide essential goods and services to everybody, and sell people homes or cars based on their contributions to society, whilst simultaneously guiding people to become honest and civilised and repair humanity which has been broken apart by greed, secrecy and mistrust. All it takes is for people to accept payment in //c.</p>
"",

";
    }
}