using LiveSplit.Model;
using LiveSplit.Model.Input;
using LiveSplit.Options;
using LiveSplit.TimeFormatters;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.Xml;
using WebSocketSharp.Server;

namespace LiveSplit.UI.Components
{
    public class ServerComponent : IComponent
    {
        public Settings Settings { get; set; }
        public WebSocketServer Server { get; set; }

        protected LiveSplitState State { get; set; }
        protected Form Form { get; set; }
        protected TimerModel Model { get; set; }
        protected ITimeFormatter DeltaFormatter { get; set; }
        protected ITimeFormatter SplitTimeFormatter { get; set; }

        protected bool AlwaysPauseGameTime { get; set; }

        public float PaddingTop => 0;
        public float PaddingBottom => 0;
        public float PaddingLeft => 0;
        public float PaddingRight => 0;

        public string ComponentName => $"LiveSplit Websocket ({ Settings.Port })";

        public IDictionary<string, Action> ContextMenuControls { get; protected set; }

        public ServerComponent(LiveSplitState state)
        {
            Settings = new Settings();
            Model = new TimerModel();

            DeltaFormatter = new PreciseDeltaFormatter(TimeAccuracy.Hundredths);
            SplitTimeFormatter = new RegularTimeFormatter(TimeAccuracy.Hundredths);

            ContextMenuControls = new Dictionary<string, Action>();
            ContextMenuControls.Add("Start Server (WS)", Start);

            State = state;
            Form = state.Form;

            Model.CurrentState = State;
            State.OnStart += State_OnStart;
        }

        public void Start()
        {
            Server = new WebSocketServer(Settings.Port);
            Server.AddWebSocketService("/livesplit", () => new Connection(connection_MessageReceived));
            Server.Start();

            ContextMenuControls.Clear();
            ContextMenuControls.Add("Stop Server (WS)", Stop);
        }

        public void Stop()
        {
            Server.Stop();
            ContextMenuControls.Clear();
            ContextMenuControls.Add("Start Server (WS)", Start);
        }

        TimeSpan? parseTime(string timeString)
        {
            if (timeString == "-")
                return null;

            return TimeSpanParser.Parse(timeString);
        }

        void connection_MessageReceived(object sender, MessageEventArgs e)
        {
            Form.BeginInvoke(new Action(() => ProcessMessage(e.Message, e.Arguments, e.Connection)));
        }

        private void ProcessMessage(String message, string[] arguments, Connection clientConnection)
        {
            try
            {
                if (message == "registerEvent")
                {
                    if (arguments.Length == 0)
                    {

                    }
                    foreach (string ev in arguments)
                    {
                        dynamic handler;
                        switch (ev)
                        {
                            case "pause":
                                handler = new EventHandler((object o, EventArgs e) =>
                                {
                                    clientConnection.SendMessage(ev);
                                });
                                State.OnPause += handler;
                                clientConnection.addDisconnectHandler(() => State.OnPause -= handler);
                                break;
                            case "reset":
                                handler = new EventHandlerT<TimerPhase>((object o, TimerPhase e) =>
                                {
                                    clientConnection.SendMessage(ev, e.ToString());
                                });
                                State.OnReset += handler;
                                clientConnection.addDisconnectHandler(() => State.OnReset -= handler);
                                break;
                            case "resume":
                                handler = new EventHandler((object o, EventArgs e) =>
                                {
                                    clientConnection.SendMessage(ev);
                                });
                                State.OnResume += handler;
                                clientConnection.addDisconnectHandler(() => State.OnResume -= handler);
                                break;
                            case "skipSplit":
                                handler = new EventHandler((object o, EventArgs e) =>
                                {
                                    clientConnection.SendMessage(ev);
                                });
                                State.OnSkipSplit += handler;
                                clientConnection.addDisconnectHandler(() => State.OnSkipSplit -= handler);
                                break;
                            case "split":
                                handler = new EventHandler((object o, EventArgs e) =>
                                {
                                    clientConnection.SendMessage(ev);
                                });
                                State.OnSplit += handler;
                                clientConnection.addDisconnectHandler(() => State.OnSplit -= handler);
                                break;
                            case "start":
                                handler = new EventHandler((object o, EventArgs e) =>
                                {
                                    clientConnection.SendMessage(ev);
                                });
                                State.OnStart += handler;
                                clientConnection.addDisconnectHandler(() => State.OnStart -= handler);
                                break;
                            case "switchComparison":
                                handler = new EventHandler((object o, EventArgs e) =>
                                {
                                    clientConnection.SendMessage(ev, State.CurrentComparison);
                                });
                                State.OnSwitchComparisonNext += handler;
                                State.OnSwitchComparisonPrevious += handler;
                                clientConnection.addDisconnectHandler(() =>
                                {
                                    State.OnSwitchComparisonNext -= handler;
                                    State.OnSwitchComparisonPrevious -= handler;
                                });
                                break;
                            case "undoAllPauses":
                                handler = new EventHandler((object o, EventArgs e) =>
                                {
                                    clientConnection.SendMessage(ev);
                                });
                                State.OnUndoAllPauses += handler;
                                clientConnection.addDisconnectHandler(() => State.OnUndoAllPauses -= handler);
                                break;
                            case "undoSplit":
                                handler = new EventHandler((object o, EventArgs e) =>
                                {
                                    clientConnection.SendMessage(ev);
                                });
                                State.OnUndoSplit += handler;
                                clientConnection.addDisconnectHandler(() => State.OnUndoSplit -= handler);
                                break;
                        }
                    }
                }
                if (message == "startorsplit")
                {
                    if (State.CurrentPhase == TimerPhase.Running)
                    {
                        Model.Split();
                    }
                    else
                    {
                        Model.Start();
                    }
                }
                else if (message == "split")
                {
                    Model.Split();
                }
                else if (message == "unsplit")
                {
                    Model.UndoSplit();
                }
                else if (message == "skipsplit")
                {
                    Model.SkipSplit();
                }
                else if (message == "pause" && State.CurrentPhase != TimerPhase.Paused)
                {
                    Model.Pause();
                }
                else if (message == "resume" && State.CurrentPhase == TimerPhase.Paused)
                {
                    Model.Pause();
                }
                else if (message == "reset")
                {
                    Model.Reset();
                }
                else if (message == "starttimer")
                {
                    Model.Start();
                }
                else if (message.StartsWith("setgametime "))
                {
                    var value = message.Split(' ')[1];
                    var time = parseTime(value);
                    State.SetGameTime(time);
                }
                else if (message.StartsWith("setloadingtimes "))
                {
                    var value = message.Split(' ')[1];
                    var time = parseTime(value);
                    State.LoadingTimes = time ?? TimeSpan.Zero;
                }
                else if (message == "pausegametime")
                {
                    State.IsGameTimePaused = true;
                }
                else if (message == "unpausegametime")
                {
                    AlwaysPauseGameTime = false;
                    State.IsGameTimePaused = false;
                }
                else if (message == "alwayspausegametime")
                {
                    AlwaysPauseGameTime = true;
                    State.IsGameTimePaused = true;
                }
                else if (message == "getdelta" || message.StartsWith("getdelta "))
                {
                    var comparison = State.CurrentComparison;
                    if (message.Contains(" "))
                        comparison = message.Split(new char[] { ' ' }, 2)[1];
                    var delta = LiveSplitStateHelper.GetLastDelta(State, State.CurrentSplitIndex, comparison, State.CurrentTimingMethod);
                    clientConnection.SendMessage(message.Trim(), delta.HasValue ? delta.Value.TotalMilliseconds : 0);
                }
                else if (message == "getsplits")
                {
                    clientConnection.SendMessage(message, State.Run.Select((segment) =>
                    {
                        dynamic result = new System.Dynamic.ExpandoObject();
                        result.icon = segment.Icon;
                        result.name = segment.Name;

                        var comparison = segment.Comparisons[State.CurrentComparison][State.CurrentTimingMethod];
                        if (comparison.HasValue)
                        {
                            result.comparison = comparison.Value.TotalMilliseconds;
                        }

                        var splitTime = segment.SplitTime[State.CurrentTimingMethod];
                        if (splitTime.HasValue)
                        {
                            result.splitTime = splitTime.Value.TotalMilliseconds;
                        }

                        var personalBestSplitTime = segment.PersonalBestSplitTime[State.CurrentTimingMethod];
                        if (personalBestSplitTime.HasValue)
                        {
                            result.personalBestSplitTime = personalBestSplitTime.Value.TotalMilliseconds;
                        }

                        var bestSegmentTime = segment.BestSegmentTime[State.CurrentTimingMethod];
                        if (bestSegmentTime.HasValue)
                        {
                            result.bestSegmentTime = bestSegmentTime.Value.TotalMilliseconds;
                        }
                        return result;
                    }));
                }
                else if (message == "getsplitindex")
                {
                    clientConnection.SendMessage(message, State.CurrentSplitIndex);
                }
                else if (message == "getcurrentsplitname")
                {
                    var splitindex = State.CurrentSplitIndex;
                    var currentsplitname = State.CurrentSplit.Name;
                    var response = currentsplitname;
                    clientConnection.SendMessage(message, response);
                }
                else if (message == "getprevioussplitname")
                {
                    var previoussplitindex = State.CurrentSplitIndex - 1;
                    var previoussplitname = State.Run[previoussplitindex].Name;
                    var response = previoussplitname;
                    clientConnection.SendMessage(message, response);
                }
                else if (message == "getlastsplittime" && State.CurrentSplitIndex > 0)
                {
                    var splittime = State.Run[State.CurrentSplitIndex - 1].SplitTime[State.CurrentTimingMethod];
                    clientConnection.SendMessage(message, splittime.HasValue ? splittime.Value.TotalMilliseconds : 0);
                }
                else if (message == "getcomparisonsplittime")
                {
                    try
                    {
                        TimeSpan? splittime;
                        if (State.CurrentSplit == null)
                        {
                            splittime = State.Run[0].Comparisons[State.CurrentComparison][State.CurrentTimingMethod];
                        }
                        else
                        {
                            splittime = State.CurrentSplit.Comparisons[State.CurrentComparison][State.CurrentTimingMethod];
                        }

                        var response = 0.0;
                        if (splittime.HasValue)
                        {
                            response = splittime.Value.TotalMilliseconds;
                        }
                        clientConnection.SendMessage(message, response);
                    }
                    catch (Exception e)
                    {
                        clientConnection.SendError(message, e);
                    }
                }
                else if (message == "getcurrenttime")
                {
                    var timingMethod = State.CurrentTimingMethod;
                    if (timingMethod == TimingMethod.GameTime && !State.IsGameTimeInitialized)
                        timingMethod = TimingMethod.RealTime;
                    var time = State.CurrentTime[timingMethod];
                    clientConnection.SendMessage(message, time.HasValue ? time.Value.TotalMilliseconds : 0);
                }
                else if (message == "getfinaltime" || message.StartsWith("getfinaltime "))
                {
                    var comparison = State.CurrentComparison;
                    if (message.Contains(" "))
                    {
                        comparison = message.Split(new char[] { ' ' }, 2)[1];
                    }
                    var time = (State.CurrentPhase == TimerPhase.Ended)
                        ? State.CurrentTime[State.CurrentTimingMethod]
                        : State.Run.Last().Comparisons[comparison][State.CurrentTimingMethod];
                    clientConnection.SendMessage(message.Trim(), time.HasValue ? time.Value.TotalMilliseconds : 0);
                }
                else if (message.StartsWith("getpredictedtime "))
                {
                    var comparison = message.Split(new char[] { ' ' }, 2)[1];
                    var prediction = PredictTime(State, comparison);
                    clientConnection.SendMessage(message, prediction.HasValue ? prediction.Value.TotalMilliseconds : 0);
                }
                else if (message == "getbestpossibletime")
                {
                    var comparison = LiveSplit.Model.Comparisons.BestSegmentsComparisonGenerator.ComparisonName;
                    var prediction = PredictTime(State, comparison);
                    clientConnection.SendMessage(message, prediction.HasValue ? prediction.Value.TotalMilliseconds : 0);
                }
                else if (message == "getcurrenttimerphase")
                {
                    var response = State.CurrentPhase.ToString();
                    clientConnection.SendMessage(message, response);
                }
                else if (message == "getcomparison")
                {
                    clientConnection.SendMessage(message, State.CurrentComparison);
                }
                else if (message.StartsWith("setcomparison "))
                {
                    var comparison = message.Split(new char[] { ' ' }, 2)[1];
                    State.CurrentComparison = comparison;
                }
                else if (message == "switchto realtime")
                {
                    State.CurrentTimingMethod = TimingMethod.RealTime;
                }
                else if (message == "switchto gametime")
                {
                    State.CurrentTimingMethod = TimingMethod.GameTime;
                }
                else if (message.StartsWith("setsplitname "))
                {
                    int index = Convert.ToInt32(message.Split(new char[] { ' ' }, 3)[1]);
                    string title = message.Split(new char[] { ' ' }, 3)[2];
                    State.Run[index].Name = title;
                    State.Run.HasChanged = true;
                }
                else if (message.StartsWith("setcurrentsplitname "))
                {
                    string title = message.Split(new char[] { ' ' }, 2)[1];
                    State.Run[State.CurrentSplitIndex].Name = title;
                    State.Run.HasChanged = true;
                }
                else if (message == "getGame")
                {
                    dynamic response = new System.Dynamic.ExpandoObject();
                    response.gameName = State.Run.GameName;
                    response.gameIcon = State.Run.GameIcon;
                    response.categoryName = State.Run.CategoryName;
                    response.attempts = State.Run.AttemptCount;
                    response.completedAttempts = State.Run.AttemptHistory.Where(x => x.Time.RealTime != null).Count();
                    clientConnection.SendMessage(message, response);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }
        }

        private void connection_Disconnected(object sender, EventArgs e)
        {
        }

        private void State_OnStart(object sender, EventArgs e)
        {
            if (AlwaysPauseGameTime)
                State.IsGameTimePaused = true;
        }

        private TimeSpan? PredictTime(LiveSplitState state, string comparison)
        {
            if (state.CurrentPhase == TimerPhase.Running || state.CurrentPhase == TimerPhase.Paused)
            {
                TimeSpan? delta = LiveSplitStateHelper.GetLastDelta(state, state.CurrentSplitIndex, comparison, State.CurrentTimingMethod) ?? TimeSpan.Zero;
                var liveDelta = state.CurrentTime[State.CurrentTimingMethod] - state.CurrentSplit.Comparisons[comparison][State.CurrentTimingMethod];
                if (liveDelta > delta)
                    delta = liveDelta;
                return delta + state.Run.Last().Comparisons[comparison][State.CurrentTimingMethod];
            }
            else if (state.CurrentPhase == TimerPhase.Ended)
            {
                return state.Run.Last().SplitTime[State.CurrentTimingMethod];
            }
            else
            {
                return state.Run.Last().Comparisons[comparison][State.CurrentTimingMethod];
            }
        }

        public void DrawVertical(Graphics g, LiveSplitState state, float width, Region clipRegion)
        {
        }

        public void DrawHorizontal(Graphics g, LiveSplitState state, float height, Region clipRegion)
        {
        }

        public float VerticalHeight => 0;

        public float MinimumWidth => 0;

        public float HorizontalWidth => 0;

        public float MinimumHeight => 0;

        public XmlNode GetSettings(XmlDocument document)
        {
            return Settings.GetSettings(document);
        }

        public Control GetSettingsControl(LayoutMode mode)
        {
            return Settings;
        }

        public void SetSettings(XmlNode settings)
        {
            Settings.SetSettings(settings);
        }

        public void Update(IInvalidator invalidator, LiveSplitState state, float width, float height, LayoutMode mode)
        {
        }

        public void Dispose()
        {
            State.OnStart -= State_OnStart;
            Server.Stop();
        }

        public int GetSettingsHashCode()
        {
            return Settings.GetSettingsHashCode();
        }
    }
}
