About Civil Money
===========
Civil Money is an open source debt-free monetary framework which aims to become a unified global currency that can guide us towards a more civilised society. It includes features such as:
- A generous universal basic income. 
- A democratic voting process for any fundamental changes to the system.
- A low barrier to entry.
- Seeding based on regional productivity (inverse taxation.) 
- Transparent transactions and accountability.
- Implicit dispute resolution.
- A consensus-based scalable distributed P2P architecture.
- An efficient and easy to work with messaging format.
- End-to-end TLS between all peers and user clients.


General Inspirations and Design Guidelines
==========================================

### 1. Money is basically valueless.
We need to stop thinking about money as some mystical/scarce resource - it's not. These days it's just [SQL data](http://money.howstuffworks.com/currency6.htm). 
[97% of money in circulation](http://positivemoney.org/issues/debt/) is created endogenously by banks when they extend 
loans and credit. It used to be that [reserve requirements](https://en.wikipedia.org/wiki/Reserve_requirement) were placed upon this system to stop things getting out of hand, but that is no longer true today in most countries. If that isn't absurd enough, derivative "number games" are played shuffling all of the generated SQL data (debt) around, to the general
detriment and counter-production to the majority in society. 

Civil Money is a new fiat for people who are ready to try a completely new monetary system. A truly unified global effort to rebuild communities, given the past 200 years of economic adolescence leading current civilisation astray.

### 2. You are the bank.
Any person can extend credit to any other person simply by accepting 
their Civil Money payment. Even if the buyer's balance is in the negative, or their credit score is low. Centralised banking institutions as well as loans, 
are unnecessary by design. There is no financial motivation for a seller to decline a customer's payment. You always get paid either way. The only factor at play here is whether or not the customer, be it a person or business entity, appears
genuinely deserving of your goods or services. 

### 3. Minimal barrier to entry.
The only barrier to entry is *temporary* access to the internet. Meaning any reasonably modern desktop or mobile web browser. Civil Money should work just as well for a remote community in Kenya sharing a single smartphone as it will a person standing at a point of sale terminal.

We do not restrict the creation of new accounts through governmental oversights, or require any forms of identification such as birth certificates etc. Firstly, many developing nations in the world simply have no such data or processing capabilities in place. Secondly, it would place a strong importance on the shear existence of every particular account - *"this is me, if I lose this, I'm screwed."* Thirdly, we need to avoid storage of anything that can be used for identity theft. 

It is better if the monetary system is designed such that individual accounts are not so important in the big picture. A brand new account is just as good as an old one for essential day-to-day purchases. If you develop amnesia and forget your pass phrase, it's not the end of the world. Make a new account, set your income eligibility as "Health Problem", write down your pass phrase, move on with your life.

True identity authentication is *not* feature of the Civil Money framework. Instead it takes a hands-off approach, implementing implicit dispute resolution (get your money back yourself) taking disputes out of the equation, whilst gently steering people toward good behaviour.

### 4. We assume "most" accounts will act in good faith. 
One long-term study in particular suggests that people are generally well behaved when merely reminded of their
moral compass (see [Prof. Dan Ariely, (dis)honesty - the truth about lies](http://thedishonestyproject.com/film/).) We assume this somewhat going to be the general case. Ultimately it is up to society to ignore or remind those who habitually misbehave about the Civil Money Honour Code.


### 5. Misbehaving accounts should minimally impact legitimate accounts. 
The idea is, "congratulations idiot, you've made a useless account and sent yourself a bunch of money, good for you." We need to remind people that money means nothing in the first place. What matters is "are you a genuinely decent human being", or "for what reason does this guy NOT deserve to be able to buy the thing I'm selling"? The answer is almost always "no reason" or "I just don't have any left to sell".  

### 6. //c 1.00 always equals 1hr of labour, but also USD $50
Civil Money is a *hybrid* time based currency. Inflation is prevented in Civil Money because its value is pegged to a constant of time. However, the suggested value of //c 1.00 is also USD $50. In other words, an average wage should be $50/hr.

This is based on an upper-middle class USD$ 80,000/yr income over an 8hr work day, 200 days a year (excludes 165 days of weekends/personal/sick/vacation time.)
USD$ 80,000 / 1600hrs = $50/hr.
Since 1hr = //p 1.00 it follows that //p 1.00 = USD$50.


### 7. Double-spend is allowed.
Because money means basically nothing, there is no reason why we can't have implicit dispute resolution. Meaning you can dispute a transaction if a product or service was bad, and both parties will retain their money (the dispute is settled amicably by default.) 

To prevent inflation through this mechanism, it reflects badly on users who abuse the system. That is, sellers who frequently do not volunteer a refund during dispute, or a customer who disputes a lot of their purchases.

### 8. Servers are never trusted.
A consensus algorithm is always used to corroborate account, transaction and voting data. 

Because data is stored in a Distributed Hash Table based on IP address, it is difficult to insert a malicious server at a specific network end-point as to influence the consensus about any particular target account. The more well behaved peers on the network, the more resilient it becomes.

At the end of the day, *somebody* has to securely deliver a trusted client application that will adhere to all protocols and corroborate data correctly. The https://civil.money endpoint is provided for this reason, however it is currently a single point of failure. Native applications will eventually need to be created which do not rely on DNS. 

### 9. We use TLS but not for protecting information secrecy (there is none.)
Civil Money's use of TLS is simply to minimise MiTM attacks, javascript tampering and such. Also most mobile frameworks are beginning to demand it.


### 10. All cryptographic tasks must take place "on the client".
Pass phrases should never be cached or transmitted over the internet at any point in time for any reason. Industry standard [RFC2898 (aka PBKDF2)](https://www.ietf.org/rfc/rfc2898.txt) password key derivation is used to AES encrypt private keys. 

The key derivation scheme can be customised/upgraded over time and the private key encryption method can theoretically be up to the client implementation to decide. All clients *should* however support a set of standardised schemes so people don't need to always use one particular client application.

### 11. Civil Money must not become a forum for advertisement or communication. It is a framework for decentralised monetary exchange only.
Storage of emails and general blobs of text are disallowed by design. Even transaction memos are "under the fold" as to deter any kind of spamming activity.

### 12. Taxation is implicit and inverted and governments can access their funds.
Instead of taking money out of pocket, taxation is a money creation process under Civil Money. Meaning the death of taxes. No more periodic tax filing, and tax evasion is impossible. 
Governing authority accounts for every geographical region can be created for inverse-tax revenue spending if/when governments decide to join Civil Money. 

### 13. People can vote on changes to the system
People sign votes in the same way they do transactions. 

Researchers are encouraged to collect and analyse votes and account history/transaction patterns from across the network in order to identify "vote stuffing" accounts. 

This is a computer sciences issue, as such voting outcomes are only finalised when a reasonable margin of error is established and data has been peer-reviewed through the scientific method. 

Initially, since Civil Money is a ghost town, it is up to the steering group to do its best to arrive at the most truthful impartial result. To help with this end, a two-thirds majority win is needed for any proposition to pass and all vote tallying data is freely available for download and verification by anyone.


License
=======
Civil Money is free and unencumbered software released into the public domain ([unlicense.org](http://unlicense.org)), unless otherwise denoted in the source file.