#region License

//
// Civil Money is free and unencumbered software released into the public domain (unlicense.org), unless otherwise
// denoted in the source file.
//

#endregion License

using Bridge.Html5;
using CM.Schema;
using System;
using System.Linq;
using System.Text;

namespace CM.Javascript {

    /// <summary>
    /// The voting page displays current propositions and voting outcomes.
    /// </summary>
    internal class VotesPage : Page {
        // Display proposition "1" like "001"
        private const string NumberFormatting = "000";

        private Feedback _MainFeedback;
        private uint _Proposition;
        public VotesPage(string proposition) {
            uint.TryParse(proposition ?? String.Empty, out _Proposition);
        }

        public override string Title {
            get {
                return SR.TITLE_VOTING;
            }
        }

        public override string Url {
            get {
                return "/vote"
                    + (_Proposition != 0 ? "/" + _Proposition : "");
            }
        }

        public override void Build() {
            Element.ClassName = "votespage";
            if (_Proposition == 0) {
                // overview
                BuildOverview();
            } else {
                BuildVotePage();
            }
        }

        private static void FixJsonDates(Schema.VotingProposition[] ar) {
            for (int i = 0; i < ar.Length; i++) {
                var p = ar[i];
                // these will come in as strings, even though the CLR type is DateTime
                p.CloseUtc = Helpers.DateFromISO8601(p.CloseUtc.As<string>());
                p.CreatedUtc = Helpers.DateFromISO8601(p.CreatedUtc.As<string>());
            }
        }

        private void BuildOverview() {
            var div = Element.Div();
            div.H1(SR.TITLE_VOTING);
            div.Div(null, SR.HTML_VOTES_INTRO);
            Feedback feedback = new Feedback(div);

            feedback.Set(Assets.SVG.Wait, FeedbackType.Default, SR.LABEL_STATUS_CONTACTING_NETWORK);

            App.Identity.Client.QueryAuthoritiveServer(new AsyncRequest<Client.HttpRequest>() {
                Item = new Client.HttpRequest("/api/get-propositions"),
                OnComplete = (e) => {
                    if (e.Result == CMResult.S_OK
                       && e.Item.Content != null) {
                        div.H2(SR.TITLE_CURRENT_PROPOSITIONS);
                        var active = div.Div();
                        div.H2(SR.TITLE_CLOSED_PROPOSITIONS);
                        var inactive = div.Div();

                        var data = JSON.Parse(e.Item.Content);
                        var ar = (Schema.VotingProposition[])data;
                        FixJsonDates(ar);
                        ar.JsSort((a, b) => { return ((Schema.VotingProposition)a).CloseUtc.CompareTo(((Schema.VotingProposition)b).CloseUtc); });
                        for (int i = 0; i < ar.Length; i++) {
                            var p = ar[i];
                            var details = GetBestDetails(p);
                            var row = div.Div("prop-summary");
                            row.H3("#" + p.ID.ToString(NumberFormatting) + " - " + HtmlEncode(details.Title));

                            row.Div("desc", HtmlEncode(details.Description));

                            string summary = SR.LABEL_VOTING_CLOSE_DATE + ": " + Helpers.DateToISO8601(p.CloseUtc) + " UTC"
                            + "<br/>" + SR.LABEL_VOTING_ELIGIBLE_PARTICIPANTS + ": " + (p.For + p.Against).ToString("N0")
                            + "<br/>" + SR.LABEL_VOTING_INELIGIBLE_UNVERIFIED_PARTICIPANTS + ": " + p.Ineligible.ToString("N0");

                            row.Div("status", summary);

                            double tot = p.Against + p.For + p.Ineligible;
                            var graph = row.Div("graph").Div();
                            var votesFor = graph.Div("for");
                            var forPer = (tot != 0 ? p.For / tot : 0) * 100;
                            var againstPer = (tot != 0 ? p.Against / tot : 0) * 100;
                            var ineligiblePer = ((tot + p.Ineligible) != 0 ? p.Ineligible / tot : 0) * 100;
                            votesFor.Div(null, "<span>" + forPer.ToString("N1") + "%</span>").Style.Height = forPer + "%";
                            votesFor.H3(p.For.ToString("N0") + "<br/>" + SR.LABEL_VOTE_FOR);

                            var votesAgainst = graph.Div("against");
                            votesAgainst.Div(null, "<span>" + againstPer.ToString("N1") + "%</span>").Style.Height = againstPer + "%";
                            votesAgainst.H3(p.Against.ToString("N0") + "<br/>" + SR.LABEL_VOTE_AGAINST);

                            var votesIneligible = graph.Div("ineligible");
                            votesIneligible.Div(null, "<span>" + ineligiblePer.ToString("N1") + "%</span>").Style.Height = ineligiblePer + "%";
                            votesIneligible.H3(p.Ineligible.ToString("N0") + "<br/>" + SR.LABEL_VOTE_INELIGIBLE);

                            var buttons = row.Div("button-row");
                            if (p.CloseUtc > DateTime.UtcNow) {
                                buttons.Button(SR.LABEL_LEARN_MORE_OR_VOTE, "/vote/" + p.ID);
                            }
                            buttons.Button(SR.LABEL_DOWNLOAD_DATA, (x) => {
                                var url = x.Target.GetAttribute("url");
                                Window.Open(url);
                            }).SetAttribute("url", App.Identity.Client.CurrentAuthoritativeServer + "/api/get-vote-data?proposition-id=" + p.ID);

                            // It doesn't matter if the user's clock is incorrect here. Vote tallying
                            // is a reporting process. Any votes after the designated time are simply ineligible.
                            if (p.CloseUtc <= DateTime.UtcNow) {
                                inactive.AppendChild(row);
                            } else {
                                active.AppendChild(row);
                            }
                        }

                        feedback.Hide();

                        if (active.ChildNodes.Length == 0) {
                            active.Div(null, SR.LABEL_VOTES_NO_PROPOSITIONS);
                        }
                        if (inactive.ChildNodes.Length == 0) {
                            inactive.Div(null, SR.LABEL_VOTES_NO_PROPOSITIONS);
                        }
                    } else {
                        feedback.Set(Assets.SVG.Warning, FeedbackType.Default,
                            SR.LABEL_STATUS_PROBLEM_REACHING_A_SERVER);
                    }
                }
            });
        }
        HTMLElement _Top;

        private void BuildVotePage() {
            Element.ClassName = "castvotepage";
            var div = Element.Div();
            _Top = div.Div("top");
            _Top.H1(String.Format(SR.TITLE_PROPOSITION_NUMBER, _Proposition.ToString(NumberFormatting)));

            _MainFeedback = new Feedback(div, big: true);
            _MainFeedback.Set(Assets.SVG.Wait, FeedbackType.Default, SR.LABEL_STATUS_CONTACTING_NETWORK);

            App.Identity.Client.QueryAuthoritiveServer(new AsyncRequest<Client.HttpRequest>() {
                Item = new Client.HttpRequest("/api/get-propositions"),
                OnComplete = (e) => {
                    if (e.Result == CMResult.S_OK
                       && e.Item.Content != null) {
                        var data = JSON.Parse(e.Item.Content);
                        var ar = (Schema.VotingProposition[])data;
                        FixJsonDates(ar);
                        var p = ar.FirstOrDefault(x => x.ID == _Proposition);
                        if (p == null) {
                            _MainFeedback.Set(Assets.SVG.Warning, FeedbackType.Default, SR.TITLE_NOT_FOUND);
                            return;
                        }
                        RenderPopositionPage(p);
                        _MainFeedback.Hide();
                    } else {
                        _MainFeedback.Set(Assets.SVG.Warning, FeedbackType.Default,
                            SR.LABEL_STATUS_PROBLEM_REACHING_A_SERVER);
                    }
                }
            });
        }

        private Schema.VotingProposition.TranslatedDetails GetBestDetails(Schema.VotingProposition p) {
            for (int i = 0; i < p.Translations.Length; i++) {
                if (String.Compare(SR.CurrentLanguage, p.Translations[i].Code, true) == 0)
                    return p.Translations[i];
            }
            return p.Translations[0];
        }
        private void RenderPopositionPage(Schema.VotingProposition p) {
            var div = Element.Div();
           
            var details = GetBestDetails(p);

            _Top.H2(HtmlEncode(details.Title));
            _Top.Div(null, HtmlEncode(details.Description));
            var row = div.Div("row");
            var left = row.Div("cell-half");
            var right = row.Div("cell-half");

            left.H3(SR.TITLE_KNOWN_NEGATIVE_IMPACTS);
            var tmp = (details.NegativeImpacts ?? String.Empty).Split('\n');
            var s = new StringBuilder();
            s.Append("<ul>");
            for (int x = 0; x < tmp.Length; x++)
                s.Append("<li>" + HtmlEncode(tmp[x]) + "</li>");
            s.Append("</ul>");
            left.Div(null, s.ToString());

            right.H3(SR.TITLE_KNOWN_POSITIVE_IMPACTS);
            tmp = (details.PositiveImpacts ?? String.Empty).Split('\n');
            s.Clear();
            s.Append("<ul>");
            for (int x = 0; x < tmp.Length; x++)
                s.Append("<li>" + HtmlEncode(tmp[x]) + "</li>");
            s.Append("</ul>");
            right.Div(null, s.ToString());

            row = div.Div("row");
            left = row.Div("cell-half");
            right = row.Div("cell-half");
            var myAccountTitle = left.H3(SR.LABEL_MY_ACCOUNT);
            var account = new AccountInputBox(left, goGlyph: false);
            Feedback feedback = new Feedback(div);

            Schema.Vote _ExistingVote = null;

            var myVoteTitle = right.H3(SR.LABEL_MY_VOTE);

            var rdoFor = right.RadioButton("vote", SR.LABEL_VOTE_FOR);
            var rdoAgainst = right.RadioButton("vote", SR.LABEL_VOTE_AGAINST);
       
            var signingBox = new SigningBox(div);

            var passRow = div.Div("row");
  
            var returnButtons = Element.Div();
            var serverStatus = Element.Div("statusvisual");
            var submit = passRow.Div("button-row").Button(SR.LABEL_CONTINUE, (e) => {
                var a = account.Account;

                if (a == null) {
                    feedback.Set(Assets.SVG.Warning, FeedbackType.Error,
                        SR.LABEL_YOUR_ACCOUNT_NAME_IS_REQUIRED);
                    myAccountTitle.ScrollIntoView(true);
                    return;
                }

                if (!rdoFor.Checked
                    && !rdoAgainst.Checked) {
                    feedback.Set(Assets.SVG.Warning, FeedbackType.Error,
                      SR.LABEL_YOUR_VOTE_SELECTION_IS_REQUIRED);
                    myVoteTitle.ScrollIntoView(true);
                    return;
                }
                feedback.Hide();

                div.Style.Display = Display.None;

                var now = DateTime.UtcNow;
                var vote = new Schema.Vote() {
                    VoterID = a.ID,
                    APIVersion = 1,
                    PropositionID = (uint)_Proposition,
                    Value = rdoFor.Checked,
                    CreatedUtc = (_ExistingVote != null ? _ExistingVote.CreatedUtc : now),
                    UpdatedUtc = now,
                };

                var sign = new AsyncRequest<Schema.DataSignRequest>();
                sign.Item = new Schema.DataSignRequest();
                sign.Item.Transforms.Add(new Schema.DataSignRequest.Transform(vote.GetSigningData()));
                sign.Item.PasswordOrRSAPrivateKey = signingBox.PasswordOrPrivateKey;
                sign.OnComplete = (signRes) => {
                    if (signRes.Result == CMResult.S_OK) {
                        vote.Signature = signRes.Item.Transforms[0].Output;

                        // Validate
                        var verify = new AsyncRequest<DataVerifyRequest>() {
                            Item = new DataVerifyRequest() {
                                DataDateUtc = vote.UpdatedUtc,
                                Input = vote.GetSigningData(),
                                Signature = vote.Signature
                            }
                        };
                        a.VerifySignature(verify, JSCryptoFunctions.Identity);
                        if (verify.Result == CMResult.S_OK) {
                            // commit
                            _MainFeedback.Set(Assets.SVG.Wait,
                                FeedbackType.Default,
                                SR.LABEL_STATUS_CONTACTING_NETWORK + " ...");
                            serverStatus.Clear();
                            var prog = new ServerProgressIndicator(serverStatus);
                            prog.SetMainGlyph(Assets.SVG.Wait);
                            prog.Show();
                            var put = new AsyncRequest<PutRequest>() {
                                Item = new PutRequest(vote) { UI = prog },
                                OnComplete = (sender) => {
                                    var req = sender as AsyncRequest<PutRequest>;
                                    req.Item.UpdateUIProgress();
                                    if (req.Result == CMResult.S_OK) {
                                        _MainFeedback.Set(Assets.SVG.CircleTick, FeedbackType.Success,
                                               SR.LABEL_VOTE_SUBMITTED_SUCCESSFULLY);
                                        var options = returnButtons.Div("button-row center");
                                        options.Button(SR.LABEL_CONTINUE, "/vote");
                                        options.Button(SR.LABEL_GO_TO_YOUR_ACCOUNT, "/" + a.ID);
                                        prog.SetMainGlyph(Assets.SVG.CircleTick);
                                    } else {
                                        _MainFeedback.Set(Assets.SVG.CircleError, FeedbackType.Error,
                                            SR.LABEL_STATUS_A_PROBLEM_OCCURRED
                                            + ": " + req.Result.GetLocalisedDescription());
                                        div.Style.Display = Display.Block;
                                        prog.SetMainGlyph(Assets.SVG.CircleError);
                                    }
                                },
                                OnProgress = (sender) => {
                                    var req = sender as AsyncRequest<PutRequest>;
                                    req.Item.UpdateUIProgress();
                                }
                            };
                            App.Identity.Client.TryPut(put);
                            return;
                        }
                        // signing error..
                    }

                    div.Style.Display = Display.Block;
                    _MainFeedback.Set(Assets.SVG.Warning,
                        FeedbackType.Error,
                        SR.LABEL_STATUS_SIGNING_FAILED);
                };

                _MainFeedback.Set(Assets.SVG.Wait, FeedbackType.Default,
                    SR.LABEL_STATUS_SIGNING_INFORMATION + " ...");

                account.Account.SignData(sign, JSCryptoFunctions.Identity);
            }, className: "green-button");

            submit.Style.Display = Display.None;
            account.OnAccountChanged = (a) => {

                signingBox.Signer = a;

                if (a == null) {
                    _ExistingVote = null;
                    return;
                }
              
                feedback.Set(Assets.SVG.Wait, FeedbackType.Default, SR.LABEL_STATUS_CONTACTING_NETWORK);
                App.Identity.Client.TryFindVote(new AsyncRequest<FindVoteRequest>() {
                    Item = new FindVoteRequest(a.ID, _Proposition),
                    OnComplete = (e) => {
                        if (e.Result.Success) {
                            _ExistingVote = e.Item.Output;
                            string msg = "";
                            if (a.AccountCalculations == null || !a.AccountCalculations.IsEligibleForVoting) {
                                msg += SR.LABEL_YOU_ARE_NOT_PRESENTLY_ELIGIBLE_FOR_VOTING + "\n";
                            }
                            if (_ExistingVote != null) {
                                msg += String.Format(SR.LABEL_YOUR_LAST_VOTE_OF_BLANK_WAS_ON_BLANK,
                                    _ExistingVote.Value ? SR.LABEL_VOTE_FOR : SR.LABEL_VOTE_AGAINST,
                                    Helpers.DateToISO8601(_ExistingVote.UpdatedUtc)) + "\n";
                            }
                            if (msg.Length > 0) {
                                feedback.Set(Assets.SVG.Speech, FeedbackType.Default, msg);
                                return;
                            }
                        }
                        _ExistingVote = null;
                        feedback.Hide();
                    }
                });
            };
            signingBox.OnPasswordReadyStateChanged = (isChecked) => {
                submit.Style.Display = isChecked ? Display.Inline : Display.None;
            };
            signingBox.OnPasswordEnterKey = submit.Click;
        }
    }
}