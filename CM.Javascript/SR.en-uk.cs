#region License

//
// Civil Money is free and unencumbered software released into the public domain (unlicense.org), unless otherwise
// denoted in the source file.
//

#endregion License

namespace CM.Javascript {
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
LABEL_CIVIL_MONEY_SUB_HEADING,A civilised monetary framework.,
LABEL_ACCOUNT_NAME,Account Name,
LABEL_GO,Go,
HTML_CIVIL_MONEY_PROVIDES,""<h3>Civil Money gives you</h3>
<ul>
<li>A universal basic income</li>
<li>Seeding based on regional productivity (inverse taxation)</li>
<li>Transparent transactions and accountability</li>
<li>A more civilised cash-free society</li>
</ul>"",
LABEL_CREATE_MY_ACCOUNT,Create my account,
TITLE_PEERS,Peers,
TITLE_REGIONS,Regions,
TITLE_HISTORY,History,
TITLE_HOMEPAGE,Home,
TITLE_REGISTER,Register,
TITLE_HELP,Help,
TITLE_CHOOSE_YOUR_LANGUAGE,Choose your language,
LABEL_CHOOSE_YOUR_LANGUAGE,""We apologise that not all languages are available. If you are interested in helping out, please contact us."",
LABEL_REGISTER_INTRO,""Civil Money is not like traditional online banking systems or fiat currencies. It features a credit rating system based on your publicly visible contributions to society (income) and personal circumstance.
Account information is kept in multiple locations around the globe and you will protect the authenticity and ownership of your account through the use of a cryptographic pass phrase. Your pass phrase should never be transmitted over the internet.
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
LABEL_REQUEST_A_PAYMENT,Request a payment,
LABEL_EDIT_ACCOUNT,Edit account,
LABEL_ACCEPTANCE_LOGOS,Acceptance logos,
LABEL_SKILLS_AND_SERVICES,Skills & Services,
TITLE_TRANSACTION_HISTORY,Transaction History,
LABEL_LOADING_PLEASE_WAIT,""Loading, please wait..."",
LABEL_REPUTATION_GOOD,Good Standing,
LABEL_REPUTATION_OVERSPENT,Overspent,
LABEL_REPUTATION_BAD,Bad,
LABEL_NEW_PASSWORD_INSTRUCTIONS,""You'll need to re-enter this any time you do anything. Use multiple words to make up a memorable phrase. Complexity is not as important as overall length - an all lowercase sentence with spaces is fine."",
HTML_I_PROMISE_TO_FOLLOW_THE_HONOUR_CODE, ""I promise to follow the <b>Civil Money Honour Code</b>"",
HTML_CIVIL_MONEY_HONOUR_CODE,""<ol>
<li>I will try my best to not be a jerk. If somebody is being one to me, I will either ignore them or politely remind them about the Civil Money Honour Code.</li>
<li>I will respect any person's decision to decline my payment, regardless of reason or for no reason at all.</li>
<li>I will hold in the highest esteem any person doing an unpleasant job in exchange for Civil Money and endeavour to eliminate unpleasant jobs through the sharing of ideas, science and ingenuity.</li>
<li>I accept that Civil Money is backed only by the community at large and holds no intrinsic value, and is also an imperfect system. As such, the higher the cost of a good or service, the closer I will scrutinise a person's credit rating and transaction history, just as banks do today for a loan.</li>
<li>I accept that the value of all Civil Money transactions begin to depreciate to zero after 12 months, which helps to stimulate the economy, aids in the prevention of inflation, over accumulation of money, and forgives people of their debts to society.</li>
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
LABEL_CIVIL_MONEY_SECURITY_REMINDER,""If your web browser's address bar is not visible or its URL does not begin withhttps://civil.money/ or if in the future you don't receive this reminder, pleasedo not type in your pass phrase, as the page you are on might be trying to steal your account."",
HTML_IVE_CHECKED_MY_WEB_BROWSER_ADDRESS,I've checked my web browser's address and it definitely begins with <b>https://civil.money/</b>,
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
HTML_ABOUT,""Civil Money is a debt-free monetary system. <br/><br/>The end-game for Civil Money is to enable every person on earth to be able to raise their
family properly or get on with doing whatever it is they're truly passionate about or good
at, regardless of whether or not that skill is in high demand or pays a regular income. If
you're musician, perform. If you're a mother, raise your children. If you're a builder, build
something awesome. If you're a researcher, solve a problem. Not for the money, but
because you think it's worthwhile. A universal basic income frees every able-bodied person
to progress humanity as whole, instead of this absurd and relentless pursuit of money,
which is nothing more than a database entry sitting on your bank's computer. We are
wasting our entire lives, killing one another, compromising on our morals, all because
we've created an unnecessary artificial need of money to survive.<br/><br/>With a purely algorithmic and reputation-based approach to wealth distribution,
non-productive and destructive for-profit-only industries could fall by the wayside. Every
user of Civil Money is in effect a money lender. There is nothing preventing you from
accepting money from a person with a low credit score or even a negative balance -- you get paid
either way by simply accepting their payment. The only question to ask is, <em>should you</em>.
Predatory or deceitful users should be declined and politely reminded of the Civil Money Honour Code.
The level of compassion towards a customer's personal circumstance is up to each individual business or seller.<br/><br/>An inverse-taxation system ensures that geographic regions generate tax revenue based
on their actual economic activity in near real-time, and without relying on individual reporting. 10%
of every transaction generates new money for the region of the seller for use by a Governing Authority
Civil Money account.<br/><br/>The Civil Money monetary system is not a platform for communication or advertisement. As such
posting of e-mail addresses, phone numbers, personal information or methods of contact are
expressly discouraged. Dispute resolution is implicit in the system's design and ensures
that disputed transactions are always settled amicably, even when both parties disagree."",
TITLE_ACCOUNT_SETTINGS,Account Settings,
LABEL_ACCOUNT_SETTINGS_INTRO,""Account settings are designed to show minimal personally identifying informationwhilst still providing some sort of context about your role in the community."",
LABEL_INCOME_ELIGIBILITY_INTRO,""There is no reason to lie here. Good sellers should accept your payment for essential items if your transaction history is reasonable regardless of balance. Health Problem and Retired status does not give you a free pass to overspend your basic income."",
LABEL_SKILLS_AND_SERVICES_INTRO,""List your skills, services or anything you can do within your community, even if only in an amateur capacity."",
LABEL_ADD_ANOTHER_ITEM,Add another,
LABEL_PUSH_NOTIFICATIONS,Push Notifications,
LABEL_PUSH_NOTIFICATIONS_INTRO,""Specify one or more HTTP end-points to receive push notifications
any time your account is changed or a transaction is updated."",
LABEL_CHANGE_MY_PASS_PHRASE,Change my secret pass phrase,
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
LABEL_TAG,Tag,
LABEL_STATUS_TRANSACTION_CREATED_SUCCESSFULLY,Transaction created successfully.,
LABEL_STATUS_TRANSACTION_UPDATED_SUCCESSFULLY,Transaction updated successfully.,
LABEL_STATUS_NO_TRANSACTIONS_UPDATED,None of the transactions could be updated.,
LABEL_STATUS_ALL_TRANSACTIONS_UPDATED,All transaction were updated successfully.,
LABEL_STATUS_SOME_TRANSACTIONS_FAILED,Some of the transactions could not be updated.,
LABEL_LINK_FOR_PAYMENT_TO,Link for payment to {0},
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
LABEL_MATCHING_TRANSACTION_RECEIVED_FROM,Matching transaction received from,
LABEL_POINT_OF_SALE,Point of Sale,
TITLE_THE_CIVIL_MONEY_HONOUR_CODE,The Civil Money Honour Code,
HTML_ABOUT_1_1, ""Imagine if you didn't have to work."",
HTML_ABOUT_1_2, ""What would you do?"",
HTML_ABOUT_2_1, ""Today we work because we need money to survive."",
HTML_ABOUT_2_2, ""But why does it have to be this way?"",
HTML_ABOUT_3_1, ""What if instead, everybody had a generous basic allowance and we were free to pursue whatever it is we're truly passionate about?"",
HTML_ABOUT_3_2, ""Today's monetary system is based upon debt and an illusion of scarcity, which if you think about it, seems rather counter-productive and uncivilised.<br/><br/>Let's try something else..."",
HTML_ABOUT_4_1, ""Every person whether retired, studying, disabled or working receives a generous basic income."",
HTML_ABOUT_4_2, ""Roughly equivalent to USD $60,000 /yr. The idea is that <b>if</b> you work, it is gravy. Work on something because you're passionate about it, not because you have to. Raise your kids properly. Go to school. Do something amazing. If the people and local businesses that you rely on for day-to-day living all choose to accept payment in Civil Money, the decision will be up to you."",
HTML_ABOUT_5_1, ""There are no more banks, foreign exchange rates or financial speculation markets."",
HTML_ABOUT_5_2, ""Every user of Civil Money is a money lender. By accepting payment from a person or business, even if they have a low credit score or a negative account balance, you are extending trust. You get paid either way by simply clicking 'accept' on a payment, but the question to ask is, <em>&quot;should I?&quot;</em> <b>You are now the bank</b>, and the level of compassion towards a customer's personal circumstance is up to each individual business or seller."",
HTML_ABOUT_6_1, ""There is no such thing as physical cash."",
HTML_ABOUT_6_2, ""There are only publicly visible and verifiable balances and credit scores, which imply a level community good-standing. Credit scores offer an insight at a glance for low value everyday purchases, whilst in-depth transaction analysis and reporting can more thoroughly validate an account's legitimacy for higher priced transactions."",
HTML_ABOUT_7_1, ""The value of one Civil Money is always equal to one hour of a person's time."",
HTML_ABOUT_7_2, ""The mathematical constant of time can help prevent inflation, however goods and services must be priced appropriately. Civil Money only works if we spend wisely and scrutinise the fair value of items and services that we're purchasing (labour + materials + a reasonable margin) to stop things getting out of hand.
<br/><br/>A reasonable exchange rate in traditional currency is <b>//c 1.00 = USD $50</b>. This is based on an upper-middle class USD$ 80,000/yr income over an 8hr work day, 200 days a year (excludes 165 days of weekends/personal/sick/vacation time.)
<blockquote>USD$ 80,000 / 1600hrs = $50/hr.<br />
Since 1hr = //p 1.00 it follows that //p 1.00 = USD$50</blockquote>
This means that a person making designer T-shirts in Bangladesh, which might take a few hours, can no longer be expected to sell their time for a pittance or be compelled to work for a slave wage. Provided that they have access to materials and a personal website, that person can now sell their shirts directly to anybody in the world for a fair value of //p 3.00, equivalent to USD$150, or about what a retail chain might charge in western countries today."",
HTML_ABOUT_8_1, ""Payments begin to depreciate after 12 months."",
HTML_ABOUT_8_2, ""This means it is impossible for a minority to hoard cash as they do today. Instead, everybody is encouraged to spend their money soon, which ensures the distribution of wealth and a happy and healthy economy.<br/>
<br/>Capitalism is not prohibited, however your capital can no longer be held long-term in the form of money. Instead it must be converted into physical assets and property."",
HTML_ABOUT_9_1, ""Because payments depreciate, your credit score automatically restores itself if you fall on hard times."",
HTML_ABOUT_9_2, ""Depreciation of account debits along with a perpetually replenishing basic income ensures that people retain their dignity in the event of financial disaster. Nobody needs to struggle without access to essential goods and services for an extended period of time, if at all."",
HTML_ABOUT_10_1, ""Inverse-taxation generates revenue for regions based on economic activity."",
HTML_ABOUT_10_2, ""Tax evasion is impossible, we don't subtract money out of pocket and there is never any tax filing.<br/><br/>10% of every settled transaction is automatically generated and placed into an authoritative Civil Money account for the seller's geographical region. Any change to the inverse-taxation algorithm will not directly impact people's account balances. Inverse-taxation is a data analysis/computer sciences problem. Specifically, we exclude transactions for inverse-tax when a money trail or account looks like it might have been deliberately created to generate false revenue."",
HTML_ABOUT_11_1, ""All account and transaction information is public and distributed around the world."",
HTML_ABOUT_11_2, ""Data is stored on random untrusted computers and authenticity is established through a consensus model and well-established cryptographic signing techniques.<br/>
<br/>There is nothing novel or unique about Civil Money's technology.<br/><br/>
Because all data is public, it cannot be used for crime. Not that crime need exist in the first place, given the generous basic income. Predatory or deceitful users should be declined, ignored and politely reminded of the Civil Money Honour Code."",
HTML_ABOUT_12_1, ""Every person can vote on changes to the system."",
HTML_ABOUT_12_2, ""People can digitally sign votes in the same manner as regular transactions. A two-thirds majority is needed for any proposition to pass, meaning a significant winning margin is required before any changes are introduced. No vote counts more than another, however the minimum requirement for voting is a good standing and at least one settled transaction for every 30 day period, for the past year. This is simply to deter casual vote stuffing.<br/><br/>Because Civil Money is a completely transparent system, researchers are encouraged to collect, validate and calculate results independently and report their findings. Voting outcomes are a scientific process and not locked in stone until a consensus with a reasonably low margin of error is established."",
HTML_ABOUT_13_1, ""'Double spending' is allowed. As such, dispute resolution is built-in."",
HTML_ABOUT_13_2, ""In the event of a dispute, a customer can always get their money back whilst the seller keeps their payment as well. <br/>
<br/>The catch is, it reflects badly on anybody who abuses this system, or any seller who routinely does not volunteer a refund during disputes. This creates access to fair dispute resolution in any nations that currently have no reliable legal system in place, whilst simultaneously reducing the burden on small-claims courts in the countries that do."",
HTML_ABOUT_14_1, ""Implications."",
HTML_ABOUT_14_2, ""<h3>Potential Negative Implications</h3>
<ol>
<li>Privacy doesn't exist, meaning there is a potential for doxing or identity theft when the account ID is associated with a known physical identity. On the other hand, Civil Money users are at least cognisant of the visibility of their activities, and companies could one day end the practice of using bills as a form of identity verification.</li>
<li>Civil Money is presently open to internet blocking techniques. This can be partially addressed with the use of native/non-web browser based apps.</li>
<li>Necessity is sometimes said to be &quot;the mother of invention&quot;. Removing that necessity may or may not stall technological progress. We can only hope that it does the exact opposite and inspire people to invent amazing technological and scientific breakthroughs, because they no longer need to waste their time putting food on the table.</li>
<li>Fewer people will be willing to do unpleasant tasks. For this reason the people that do them should be held in the highest regard by the those around them. Meanwhile, we should be working on ways to eliminate unpleasant tasks through technology.</li>
<li>Sudden widespread affordability of general goods by everyone could mean increased productivity, potentially having a negative environmental impact.</li>
</ol>
<br/><br/>
<h3>Potential Positive Implications</h3>
<ol>
<li>Because you don't <em>have</em> to work, Civil Money eliminates our unsustainable profit-driven economy, which relies on imaginary short-term gains (fiat currency) at the expense of real and finite resources.</li>
<li>Poverty need no longer exist. There is not usually a compelling reason to decline a Civil Money payment from any person in need. They're always 'good for it', and our mental model of money's scarcity or perceived idea of what money is worth needn't even exist.</li>
<li>Money no longer infers power or status. One can only speculate as to the geopolitical outcome where every country and person within those countries are on an even playing field.</li>
<li>Wealth and quality of life is no longer generally predetermined by birth family and geographical place of birth.</li>
<li>Monetary based crimes are both unnecessary and trivially traced. Other types of criminal activity typically brought on through shear desperation or necessity could be greatly reduced if not eliminated.</li>
<li>Tax evasion is both impossible and unnecessary.</li>
<li>Regions are implicitly seeded revenue for social services and infrastructure based on the true local productivity of its people.</li>
<li>Inflation need not exist as //c is pegged to time/labour.</li>
<li>A unified currency reduces global trade barriers and creates fairness and equality for every person on Earth.</li>
<li>The positive environmental impact from reducing 'for profit only' resource destruction could be substantial. We hope that it will be greater than the negative impacts of increased productivity through widespread affordability.</li>
</ol>

"",
HTML_ABOUT_15_1, ""Ready for a change?"",

";
    }
}