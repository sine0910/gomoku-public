﻿using System;
using System.Collections;
using System.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;
using Firebase.Auth;
using Firebase.Firestore;
using Firebase.Analytics;
using Firebase.Database;
using Firebase.Messaging;
using Firebase.Extensions;

public class FirebaseManager : SingletonMonobehaviour<FirebaseManager>
{
    public FirebaseFirestore firestore;
    public FirebaseDatabase database;

    DataManager data_manager;

    /// <summary>
    /// 1: 성공
    /// 2: 실패/데이터가 없음
    /// 3: 실패/인터넷 에러
    /// </summary>
    /// <param name="success"></param>
    public delegate void Callback(byte success);

    void Start()
    {
        DontDestroyOnLoad(this.gameObject);

        data_manager = DataManager.instance;

        Firebase.FirebaseApp.CheckDependenciesAsync().ContinueWithOnMainThread(checkTask =>
        {
            // Peek at the status and see if we need to try to fix dependencies.
            Firebase.DependencyStatus status = checkTask.Result;
            if (status != Firebase.DependencyStatus.Available)
            {
                return Firebase.FirebaseApp.FixDependenciesAsync().ContinueWithOnMainThread(t =>
                {
                    return Firebase.FirebaseApp.CheckDependenciesAsync();
                }).Unwrap();
            } 
            else
            {
                return checkTask;
            }
        }).Unwrap().ContinueWithOnMainThread(task =>
        {
            Firebase.DependencyStatus dependencyStatus = task.Result;
            if (dependencyStatus == Firebase.DependencyStatus.Available)
            {
                // TODO: Continue with Firebase initialization.

                firestore = FirebaseFirestore.DefaultInstance;

                //LogLevel을 디버그 모드로 설정
                FirebaseFirestore.LogLevel = Firebase.LogLevel.Debug;
                firestore.Settings.PersistenceEnabled = true;

                Auth();


                //database = FirebaseDatabase.DefaultInstance;

                LoginManager.instance.check_login();
            }
            else
            {
                Debug.LogError("Could not resolve all Firebase dependencies: " + dependencyStatus);
            }
        });
    }

    #region AUTH/MESSEGE
    void Auth()
    {
        Debug.Log("Login_Auth");
        FirebaseAuth.DefaultInstance.SignInWithEmailAndPasswordAsync("", "");
    }

    public void get_notice_token()
    {
        FirebaseMessaging.MessageReceived += OnMessageReceived;
        FirebaseMessaging.TokenReceived += OnTokenReceived;
    }

    public void OnMessageReceived(object sender, MessageReceivedEventArgs e)
    {
        Debug.Log("From: " + e.Message.From);
        Debug.Log("Message ID: " + e.Message.MessageId);
    }

    public void OnTokenReceived(object sender, TokenReceivedEventArgs token)
    {
        Debug.Log("Received Registration Token: " + token.Token);

        data_manager.save_my_notice_token_data(token.Token);

        //firestore에 자신의 토큰 경로를 넣어줌
        DocumentReference docRef = firestore.Collection("Users").Document(DataManager.instance.accountID);
        Dictionary<string, object> token_data = new Dictionary<string, object>
        {
            { "NoticeToken",  token.Token }
        };
        docRef.SetAsync(token_data, SetOptions.MergeAll);
    }
    #endregion

    #region LOGIN/ACCOUNT
    public void check_account_login(string id, string pw, Callback callback)
    {
        firestore.Collection("Users").Document(id).GetSnapshotAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled)
            {
                Debug.Log("task Error: " + task.Exception);
                callback(4);
            }
            else if (task.IsCompleted)
            {
                DocumentSnapshot snapshot = task.Result;
                if (snapshot.Exists)//2021-05-07 14:37 실제 존재하는 아이디인지 확인 존재한다면 비밀번호 확인
                {
                    Dictionary<string, object> pairs = snapshot.ToDictionary();
                    if (pairs.ContainsKey("Password"))//2021-05-07 17:37 회원가입 시 인게임 데이터를 제외한 아이디와 패스워드 데이터만 저장/인게임 데이터는 게임 시작 후 저장
                    {
                        if (pw == pairs["Password"].ToString())//2021-05-07 14:40 비밀번호를 정확히 입력하였을 경우
                        {
                            callback(1);
                        }
                        else//비밀번호가 틀렸을 경우 로그인 실패
                        {
                            callback(3);
                        }
                    }
                }
                else//2021-05-07 14:38 존재하지 않은 아이디인 경우 로그인 실패/추후 계정 생성을 요구
                {
                    callback(2);
                }
            }
        });
    }

    public void check_account_id(string id, Callback callback)
    {
        firestore.Collection("Users").Document(id).GetSnapshotAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled)
            {
                Debug.Log("task Error: " + task.Exception);
                callback(3);
            }
            else if (task.IsCompleted)
            {
                DocumentSnapshot snapshot = task.Result;
                if (snapshot.Exists)//2021-07-18 13:48 실제 존재하는 아이디인지 확인 존재한다면 생성 불가능
                {
                    Dictionary<string, object> pairs = snapshot.ToDictionary();
                    callback(2);
                }
                else//2021-07-18 13:48 존재하지 않은 아이디인 경우 생성 가능한 아이디
                {
                    callback(1);
                }
            }
        });
    }

    public void save_create_account(string id, string pw, Callback callback)
    {
        Dictionary<string, object> data = new Dictionary<string, object>
        {
            { "CreateAccountDay", DateTime.Now.ToString("yyyy-MM-dd") },
            { "Password", pw },
            { "AccountID", id }
        };
        firestore.Collection("Users").Document(id).SetAsync(data, SetOptions.MergeAll).ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled)
            {
                Debug.Log("task Error: " + task.Exception);
                callback(3);
            }
            else
            {
                callback(1);
            }
        });
    }

    public void save_create_kakao_account(string id, Callback callback)
    {
        Dictionary<string, object> data = new Dictionary<string, object>
        {
            { "CreateAccountDay", DateTime.Now.ToString("yyyy-MM-dd") },
            { "AccountID", id }
        };
        firestore.Collection("Users").Document(id).SetAsync(data, SetOptions.MergeAll).ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled)
            {
                Debug.Log("task Error: " + task.Exception);
                callback(3);
            }
            else
            {
                callback(1);
            }
        });
    }

    public void upload_first_login(Callback callback)
    {
        AnalyticsLog("first_login", null, null);

        WriteBatch batch = firestore.StartBatch();

        DocumentReference my_ref = firestore.Collection("Users").Document(data_manager.accountID);
        Dictionary<string, object> data = new Dictionary<string, object>
        {
            { "FirstLogin", DateTime.Now.ToString("yyyy-MM-dd") }
        };
        batch.Set(my_ref, data, SetOptions.MergeAll);

        DocumentReference my_nuwbie_ref = firestore.Collection("Newbies").Document(data_manager.accountID);
        Dictionary<string, object> newbie_data = new Dictionary<string, object>
        {
            { "Notice", data_manager.noticeToken },

            { "LanguageCode", data_manager.language },

            { "Name", data_manager.my_name },

            { "Country", data_manager.my_country },

            { "FirstLogin", DateTime.Now.ToString("yyyy-MM-dd") }
        };
        batch.Set(my_nuwbie_ref, newbie_data);

        batch.CommitAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled)
            {
                Debug.Log("task Error: " + task.Exception);
                callback(3);
            }
            else
            {
                callback(1);
            }
        });
    }

    public void get_account_data(Callback callback)
    {
        firestore.Collection("Users").Document(data_manager.accountID).GetSnapshotAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled)
            {
                Debug.Log("task Error: " + task.Exception);
                callback(3);
            }
            else
            {
                DocumentSnapshot snapshot = task.Result;
                if (snapshot.Exists)
                {
                    try
                    {
                        //서버에 등록된 아이디의 데이터를 가져와 DataManager에 넣어주고 기기에 저장한다.
                        Dictionary<string, object> pairs = snapshot.ToDictionary();

                        data_manager.my_name = pairs["Name"].ToString();
                        data_manager.my_country = Converter.to_country((pairs["Country"].ToString()));
                        data_manager.my_tier = Converter.to_tier((pairs["Tier"].ToString()));
                        data_manager.my_old = Converter.to_old((pairs["Old"].ToString()));
                        data_manager.my_gender = Converter.to_gender((pairs["Gender"].ToString()));

                        if ((pairs.ContainsKey("BlackWin") || pairs.ContainsKey("WhiteWin") || pairs.ContainsKey("WhiteWin")) && (pairs.ContainsKey("WhiteWin") || pairs.ContainsKey("WhiteWin") || pairs.ContainsKey("WhiteWin")))
                        {
                            data_manager.b_win_count = Converter.to_int((pairs["BlackWin"].ToString()));
                            data_manager.b_lose_count = Converter.to_int((pairs["BlackLose"].ToString()));
                            data_manager.b_tie_count = Converter.to_int((pairs["BlackTie"].ToString()));

                            data_manager.w_win_count = Converter.to_int((pairs["WhiteWin"].ToString()));
                            data_manager.w_lose_count = Converter.to_int((pairs["WhiteLose"].ToString()));
                            data_manager.w_tie_count = Converter.to_int((pairs["WhiteTie"].ToString()));

                            data_manager.b_month_win_count = Converter.to_int((pairs["BlackMonthWin"].ToString()));
                            data_manager.b_month_lose_count = Converter.to_int((pairs["BlackMonthLose"].ToString()));
                            data_manager.b_month_tie_count = Converter.to_int((pairs["BlackMonthTie"].ToString()));

                            data_manager.w_month_win_count = Converter.to_int((pairs["WhiteMonthWin"].ToString()));
                            data_manager.w_month_lose_count = Converter.to_int((pairs["WhiteMonthLose"].ToString()));
                            data_manager.w_month_tie_count = Converter.to_int((pairs["WhiteMonthTie"].ToString()));

                            data_manager.b_day_win_count = Converter.to_int((pairs["BlackDayWin"].ToString()));
                            data_manager.b_day_lose_count = Converter.to_int((pairs["BlackDayLose"].ToString()));
                            data_manager.b_day_tie_count = Converter.to_int((pairs["BlackDayTie"].ToString()));

                            data_manager.w_day_win_count = Converter.to_int((pairs["WhiteDayWin"].ToString()));
                            data_manager.w_day_lose_count = Converter.to_int((pairs["WhiteDayLose"].ToString()));
                            data_manager.w_day_tie_count = Converter.to_int((pairs["WhiteDayTie"].ToString()));
                        }

                        if (pairs.ContainsKey("RatingScore"))
                        {
                            data_manager.rating_score = Converter.to_int((pairs["RatingScore"].ToString()));
                        }
                        if (pairs.ContainsKey("Heart"))
                        {
                            data_manager.my_heart = Converter.to_int((pairs["Heart"].ToString()));
                        }
                        else
                        {
                            data_manager.my_heart = 50;
                        }

                        data_manager.save_my_data();
                    }
                    catch (Exception e)
                    {
                        Debug.Log("get_account_data error!: " + e);

                        data_manager.set_default_data();
                    }
                }
                else
                {
                    data_manager.set_default_data();
                }
                callback(1);
            }
        });
    }

    public static bool check_login_end;
    public void check_login_day()
    {
        Debug.Log("check_login_day account: " + data_manager.accountID); 
        DocumentReference user_ref = firestore.Collection("Users").Document(data_manager.accountID);
        user_ref.GetSnapshotAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsCanceled || task.IsFaulted)
            {
                Debug.Log("task.IsCanceled || task.IsFaulted " + task.Exception);
            }
            else
            {
                try
                {
                    DocumentSnapshot snapshot = task.Result;
                    Debug.Log("get snapshot: " + snapshot.Id);

                    data_manager.login_time = DateTime.UtcNow.Add(TimeStamp.time_span);

                    if (snapshot.Exists && snapshot.ContainsField("Login"))
                    {
                        DateTime time = snapshot.GetValue<DateTime>("Login").Add(TimeStamp.time_span);
                        TimeStamp.compair_login_date(time);
                    }
                    else
                    {
                        data_manager.other_day = true;
                        data_manager.other_month = true;
                    }
                    initialize_date_data();
                }
                catch (Exception e)
                {
                    Debug.Log("check_login_day Error: " + e);
                }
            }
        });

        //firestore.RunTransactionAsync(transaction =>
        //{
        //    Debug.Log("on_transaction");
        //    return transaction.GetSnapshotAsync(user_ref).ContinueWithOnMainThread((task) =>
        //    {
        //        Debug.Log("on_task");

        //        try
        //        {
        //            DocumentSnapshot snapshot = task.Result;
        //            Debug.Log("get snapshot: " + snapshot.Id);

        //            if (TimeStamp.time_span == null)
        //            {
        //                Debug.Log("TimeStamp.time_span is null");
        //                TimeStamp.time_span = TimeStamp.GetTimeSpan();
        //            }

        //            //data_manager.login_time = DateTime.Now;
        //            data_manager.login_time = DateTime.UtcNow.Add(TimeStamp.time_span);

        //            //
        //            Dictionary<string, object> updates = new Dictionary<string, object>
        //            {
        //                //데이터를 받아올 때 등록한 시간이 안닌 Utc시간으로 가져오기 때문에 등록한 시간을 utc로 변경
        //                //{ "Login", DateTime.Now }
        //                { "Login", DateTime.UtcNow }
        //            };

        //            Debug.Log("DateTime.UtcNow");

        //            if (snapshot.Exists && snapshot.ContainsField("Login"))
        //            {
        //                try
        //                {
        //                    DateTime time = snapshot.GetValue<DateTime>("Login").Add(TimeStamp.time_span);

        //                    Debug.Log("Login time: " + time.ToString("yyyy-MM-dd H:mm"));
        //                    if (time.ToString("yyyy-MM-dd") != data_manager.login_time.ToString("yyyy-MM-dd"))
        //                    {
        //                        data_manager.other_day = true;

        //                        if (time.ToString("yyyy-MM") != data_manager.login_time.ToString("yyyy-MM"))
        //                        {
        //                            data_manager.other_month = true;
        //                        }
        //                    }
        //                }
        //                catch (Exception e)
        //                {
        //                    Debug.LogError("snapshot.GetValue<DateTime>(Login).Add(TimeStamp.time_span) error: " + e);
        //                    data_manager.other_day = true;
        //                    data_manager.other_month = true;
        //                }
        //            }
        //            else
        //            {
        //                Debug.Log("task");

        //                data_manager.other_day = true;
        //                data_manager.other_month = true;
        //            }

        //            if (data_manager.other_day)
        //            {
        //                updates.Add("BlackDayWin", 0);
        //                updates.Add("BlackDayLose", 0);
        //                updates.Add("BlackDayTie", 0);
        //                updates.Add("WhiteDayWin", 0);
        //                updates.Add("WhiteDayLose", 0);
        //                updates.Add("WhiteDayTie", 0);

        //                updates.Add("DayWin", 0);
        //            }
        //            if (data_manager.other_month)
        //            {
        //                updates.Add("BlackMonthWin", 0);
        //                updates.Add("BlackMonthLose", 0);
        //                updates.Add("BlackMonthTie", 0);
        //                updates.Add("WhiteMonthWin", 0);
        //                updates.Add("WhiteMonthLose", 0);
        //                updates.Add("WhiteMonthTie", 0);

        //                updates.Add("MonthWin", 0);
        //            }

        //            transaction.Set(user_ref, updates, SetOptions.MergeAll);
        //            Debug.Log("check_login_end");
        //            check_login_end = true;
        //        }
        //        catch (Exception e)
        //        {
        //            Debug.Log("check_login_day Error: " + e);
        //        }
        //    });
        //});
    }

    public void initialize_date_data()
    {
        Dictionary<string, object> updates = new Dictionary<string, object>
        {
            //데이터를 받아올 때 등록한 시간이 안닌 Utc시간으로 가져오기 때문에 등록한 시간을 utc로 변경
            { "Login", DateTime.UtcNow }
        };

        if (data_manager.other_month)
        {
            //상태값 초기화
            data_manager.other_month = false;

            data_manager.reset_month_data(); 

            updates.Add("BlackMonthWin", 0);
            updates.Add("BlackMonthLose", 0);
            updates.Add("BlackMonthTie", 0);
            updates.Add("WhiteMonthWin", 0);
            updates.Add("WhiteMonthLose", 0);
            updates.Add("WhiteMonthTie", 0);

            updates.Add("MonthWin", 0);
        }
        if (data_manager.other_day)
        {            
            //상태값 초기화
            data_manager.other_day = false;

            data_manager.reset_day_data();
            send_dailey_event();

            updates.Add("BlackDayWin", 0);
            updates.Add("BlackDayLose", 0);
            updates.Add("BlackDayTie", 0);
            updates.Add("WhiteDayWin", 0);
            updates.Add("WhiteDayLose", 0);
            updates.Add("WhiteDayTie", 0);

            updates.Add("DayWin", 0);
        }

        firestore.Collection("Users").Document(data_manager.accountID).SetAsync(updates, SetOptions.MergeAll);
    }

    public void update_my_data()
    {
        Dictionary<string, object> search_data = new Dictionary<string, object>
        {
#if !UNITY_EDITOR
            { "Version", Application.version },
#endif
            { "LanguageCode", data_manager.language },
            { "AccountID", data_manager.accountID },
            { "Name", data_manager.my_name },
            { "Country", data_manager.my_country.ToString() },
            { "Tier", data_manager.my_tier.ToString() },
            { "Old", data_manager.my_old.ToString() },
            { "Gender", data_manager.my_gender.ToString() },

            { "BlackWin", data_manager.b_win_count },
            { "BlackLose", data_manager.b_lose_count },
            { "BlackTie", data_manager.b_tie_count },
            { "WhiteWin", data_manager.w_win_count },
            { "WhiteLose", data_manager.w_lose_count },
            { "WhiteTie", data_manager.w_tie_count },

            { "Win", data_manager.b_win_count + data_manager.w_win_count },

            { "BlackMonthWin", data_manager.b_month_win_count },
            { "BlackMonthLose", data_manager.b_month_lose_count },
            { "BlackMonthTie", data_manager.b_month_tie_count },
            { "WhiteMonthWin", data_manager.w_month_win_count },
            { "WhiteMonthLose", data_manager.w_month_lose_count },
            { "WhiteMonthTie", data_manager.w_month_tie_count },

            { "MonthWin", data_manager.b_month_win_count + data_manager.w_month_win_count  },

            { "BlackDayWin", data_manager.b_day_win_count },
            { "BlackDayLose", data_manager.b_day_lose_count },
            { "BlackDayTie", data_manager.b_day_tie_count },
            { "WhiteDayWin", data_manager.w_day_win_count },
            { "WhiteDayLose", data_manager.w_day_lose_count },
            { "WhiteDayTie", data_manager.w_day_tie_count },

            { "DayWin", data_manager.b_day_win_count + data_manager.w_day_win_count  },

            { "RatingScore", data_manager.rating_score },

            { "Heart", data_manager.my_heart },

            { "PlayTime", DateTime.Now }
        };
        firestore.Collection("Users").Document(data_manager.accountID).SetAsync(search_data, SetOptions.MergeAll);
    }

    public void update_my_tier()
    {
        Dictionary<string, object> search_data = new Dictionary<string, object>
        {
            { "Tier", data_manager.my_tier.ToString() }
        };
        firestore.Collection("Users").Document(data_manager.accountID).Collection("Tier").Document("UpdateTier").SetAsync(search_data, SetOptions.MergeAll);
    }
#endregion

#region NAME
    public void can_use_this_name(string name, Callback callback)
    {
        firestore.Collection("Users").WhereEqualTo("Name", name).WhereNotEqualTo("AccountID", data_manager.accountID).GetSnapshotAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled)
            {
                Debug.Log("can_use_this_name task error: " + task.Exception);
                callback(3);
            }
            else
            {
                if (task.Result.Count != 0)
                {
                    callback(2);
                }
                else
                {
                    callback(1);
                }
            }
        });
    }
#endregion

#region MATCHING
    public void ready_to_matching(string game_room)
    {
        offline();

        WriteBatch batch = firestore.StartBatch();

        DocumentReference gameRef = firestore.Collection("MultiRoom").Document(game_room);
        Dictionary<string, object> empty = new Dictionary<string, object> { };
        batch.Set(gameRef, empty);

        DocumentReference hostRef = firestore.Collection("MultiRoom").Document(game_room).Collection("host").Document("protocol");
        Dictionary<string, object> host = new Dictionary<string, object>
        {
                { "value",  null}
        };
        batch.Set(hostRef, host);
        DocumentReference guestRef = firestore.Collection("MultiRoom").Document(game_room).Collection("guest").Document("protocol");
        Dictionary<string, object> guest = new Dictionary<string, object>
        {
                { "value",  null}
        };
        batch.Set(guestRef, guest);
        DocumentReference chatRef = firestore.Collection("MultiRoom").Document(game_room).Collection("chatting").Document("protocol");
        Dictionary<string, object> chat = new Dictionary<string, object>
        {
                { "value",  null}
        };
        batch.Set(chatRef, chat);

        batch.CommitAsync();
    }

    public void create_matching_room(string key)
    {
        Dictionary<string, object> search_data = new Dictionary<string, object>
        {
            { "key", key },
            { "roomID", data_manager.accountID },
            { "accountID", data_manager.accountID },
            { "name", data_manager.my_name },
            { "country", data_manager.my_country.ToString() },
            { "tier", data_manager.my_tier.ToString() },
            { "old", data_manager.my_old.ToString() },
            { "gender", data_manager.my_gender.ToString() },
            { "type", GameManager.instance.get_my_player_type() },
            { "version", "ver2" }
        };
        firestore.Collection("MatchingRoom").Document(data_manager.accountID).SetAsync(search_data);
    }

    public void accept_multi_game(string key, string id)
    {
        //MatchingManager가 가지고 있는 상대의 버전 정보에 맞춰 데이터를 보냄
        Dictionary<string, object> search_data = new Dictionary<string, object>
        {
            { "status", 2 +
            "/" + key +
            "/" + data_manager.accountID +
            "/" + data_manager.my_name +
            "/" + (MatchingManager.instance.version_info == "ver2" ? data_manager.my_country.ToString() : COUNTRY.KOREA.ToString()) +
            "/" + data_manager.my_tier.ToString() +
            "/" + data_manager.my_old.ToString() +
            "/" + data_manager.my_gender.ToString() }
        };
        firestore.Collection("OnlineUser").Document(id).SetAsync(search_data, SetOptions.MergeAll);
    }

    public void reject_multi_game(string key, string id)
    {
        Dictionary<string, object> search_data = new Dictionary<string, object>
        {
            { "status", 3 +
            "/" + key }
        };
        firestore.Collection("OnlineUser").Document(id).SetAsync(search_data, SetOptions.MergeAll);
    }

    public void cancle_multi_game(string key, string id)
    {
        Dictionary<string, object> search_data = new Dictionary<string, object>
        {
            { "status", 99 +
            "/" + key }
        };
        firestore.Collection("OnlineUser").Document(id).SetAsync(search_data, SetOptions.MergeAll);
    }

    public void move_scene_to_game_room(string id)
    {
        Dictionary<string, object> search_data = new Dictionary<string, object>
        {
            { "status", 6 }
        };
        firestore.Collection("OnlineUser").Document(id).SetAsync(search_data, SetOptions.MergeAll);
    }

    public void friend_matching(string id, string key)
    {
        Dictionary<string, object> search_data = new Dictionary<string, object>
        {
            { "score", null },
            { "status", 1 + "/" + key + "/" +  data_manager.accountID + "/" + data_manager.accountID + "/" 
            + data_manager.my_name + "/" + data_manager.my_country.ToString() + "/" + data_manager.my_tier.ToString() + "/" 
            + data_manager.my_old.ToString() + "/" + data_manager.my_gender.ToString() + "/" + (int)GameManager.instance.get_my_player_type() }
        };
        firestore.Collection("OnlineUser").Document(id).SetAsync(search_data);
    }

    public void initialize_my_multi_data()
    {
        online();
    }
#endregion

#region EVENT
    public void send_dailey_event()
    {
        int heart = 10;

        if (data_manager.my_rating == RATING.VIP)
        {
            heart += 20;
        }

        string title = "";

        switch (DataManager.instance.language)
        {
            case 0:
                {
                    title = "일일 출석 이벤트 보상";
                }
                break;
            case 1:
                {
                    title = "毎日の出席イベントの報酬";
                }
                break;
            case 2:
                {
                    title = "Daily Attendance Event Rewards";
                }
                break;
            case 3:
                {
                    title = "每日出席活动奖励";
                }
                break;
        }

        Dictionary<string, object> reward = new Dictionary<string, object>
        {
            { "Main", title },
            { "Reward", EVENT_ITEM.HEART.ToString() },
            { "RewardCount", heart },
            { "Deadline", DateTime.UtcNow.AddDays(1) }
        };
        firestore.Collection("Users").Document(data_manager.accountID).Collection("Event").AddAsync(reward);
    }

    public void get_event_list(Queue<Event> event_list, List<string> event_key_list)
    {
        firestore.Collection("Users").Document(data_manager.accountID).Collection("Event").GetSnapshotAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled)
            {
                Debug.Log("task Error: " + task.Exception);
            }
            else
            {
                foreach (DocumentSnapshot snapshot in task.Result.Documents)
                {
                    if (snapshot.Exists)
                    {
                        if (!event_key_list.Contains(snapshot.Id))
                        {
                            event_key_list.Add(snapshot.Id);
                            Dictionary<string, object> data = snapshot.ToDictionary();

                            if (data.ContainsKey("Main") && data.ContainsKey("Reward") && data.ContainsKey("RewardCount") && data.ContainsKey("Deadline"))
                            {
                                Debug.Log("get_event_list " + snapshot.Id);
                                string main = data["Main"].ToString();
                                EVENT_ITEM reward = Converter.to_item(data["Reward"].ToString());
                                int reward_count = Converter.to_int(data["RewardCount"].ToString());
                                DateTime event_deadline = ((Timestamp)data["Deadline"]).ToDateTime().Add(TimeStamp.time_span);

                                DateTime now_date = DateTime.Now;
                                int compare_val = DateTime.Compare(event_deadline, now_date);
                                if (compare_val > 0)
                                {
                                    event_list.Enqueue(new Event(EVENT_TYPE.PERSONAL, snapshot.Id, reward, reward_count, main, event_deadline));
                                }
                                else
                                {
                                    delete_time_out_event(snapshot.Id, main, reward, reward_count);
                                }
                            }
                        }
                    }
                }
            }
        });
    }

    public void get_server_event_list(Queue<Event> event_list, List<string> event_key_list, List<string> already_get_event_list)
    {
        firestore.Collection("ServerEvent").GetSnapshotAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled)
            {
                Debug.Log("task Error: " + task.Exception);
            }
            else
            {
                foreach (DocumentSnapshot snapshot in task.Result.Documents)
                {
                    if (snapshot.Exists)
                    {
                        Dictionary<string, object> data = snapshot.ToDictionary();

                        if (!already_get_event_list.Contains(snapshot.Id) && !event_key_list.Contains(snapshot.Id))
                        {
                            event_key_list.Add(snapshot.Id);

                            if (data.ContainsKey("Status"))
                            {
                                if (data["Status"].ToString() != "On")
                                {
                                    break;
                                }
                            }
                            string main = data["Main"].ToString();
                            EVENT_ITEM reward = Converter.to_item(data["Reward"].ToString());
                            int reward_count = Converter.to_int(data["RewardCount"].ToString());
                            DateTime event_deadline = ((Timestamp)data["Deadline"]).ToDateTime().Add(TimeStamp.time_span);

                            DateTime now_date = DateTime.Now;
                            int compare_val = DateTime.Compare(event_deadline, now_date);
                            if (compare_val > 0)
                            {
                                event_list.Enqueue(new Event(EVENT_TYPE.SERVER, snapshot.Id, reward, reward_count, main, event_deadline));
                            }
                            else
                            {
                                delete_time_out_event(snapshot.Id, main, reward, reward_count);
                            }
                        }
                    }
                }

            }
        });
    }

    public void delete_time_out_event(string key, string main, EVENT_ITEM reward, long reward_count)
    {
        firestore.Collection("Users").Document(data_manager.accountID).Collection("Event").Document(key).DeleteAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled)
            {
                Debug.Log("task Error: " + task.Exception);
            }
            else
            {
                //send_get_event_history(key, main, reward, reward_count, "TIME_OUT");
            }
        });
    }

    public void delete_all_event(string key, string main, EVENT_ITEM reward, long reward_count)
    {
        firestore.Collection("Users").Document(data_manager.accountID).Collection("Event").Document(key).DeleteAsync().ContinueWithOnMainThread(task =>
        {
            //send_get_event_history(key, main, reward, reward_count, "GET_EVENT_REWARD");
            EventManager.instance.delete_event = true;
        });
    }

    public void delete_this_event(string key, string main, EVENT_ITEM reward, long reward_count, Callback callback)
    {
        firestore.Collection("Users").Document(data_manager.accountID).Collection("Event").Document(key).DeleteAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled)
            {
                Debug.Log("task Error: " + task.Exception);
                callback(3);
            }
            else
            {
                //send_get_event_history(key, main, reward, reward_count, "GET_EVENT_REWARD");
                callback(1);
            }
        });
    }
#endregion

#region FRIEND
    public void get_my_friend_list()
    {
        Debug.Log("get_my_friend_list");
        firestore.Collection("Friends").Document(data_manager.accountID).Collection("friend").GetSnapshotAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled)
            {
                Debug.Log("can_use_this_name task error: " + task.Exception);
            }
            else
            {
                List<string> friend_id_list = new List<string>();
                foreach (var doc in task.Result)
                {
                    friend_id_list.Add(doc.Id);
                }
                data_manager.my_friend_id_list = friend_id_list;
                data_manager.save_my_friend_data();
            }
            get_my_friend_list_listening();
        });
    }

    public void get_my_friend_list_listening()
    {
        Debug.Log("get_my_friend_list_listening");
        firestore.Collection("Friends").Document(data_manager.accountID).Collection("friend").Listen(snapshot =>
        {
            foreach (DocumentChange change in snapshot.GetChanges())
            {
                try
                {
                    if (change.ChangeType == DocumentChange.Type.Removed)
                    {
                        data_manager.my_friend_id_list.Remove(change.Document.Id);
                    }
                    else
                    {
                        if (!data_manager.my_friend_id_list.Contains(change.Document.Id))
                        {
                            data_manager.my_friend_id_list.Add(change.Document.Id);
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.Log("get_my_friend_list error: " + e);
                }
            }
            data_manager.save_my_friend_data();
        });
    }

    public void check_friend_count(string accountID, Callback callback)
    {
        firestore.Collection("Friends").Document(accountID).Collection("friend").GetSnapshotAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled)
            {
                Debug.Log("can_use_this_name task error: " + task.Exception);
                callback(3);
            }
            else
            {
                if (task.Result.Count > 9)
                {
                    callback(2);
                }
                else
                {
                    callback(1);
                }
            }
        });
    }

    public void accept_invite_friend(string key, string accountID, Callback callback)
    {
        WriteBatch batch = firestore.StartBatch();

        DocumentReference otherRef = firestore.Collection("Friends").Document(accountID).Collection("friend").Document(data_manager.accountID);
        Dictionary<string, object> my_data = new Dictionary<string, object>
        {
            { "accountID", data_manager.accountID }
        };
        batch.Set(otherRef, my_data);

        DocumentReference myRef = firestore.Collection("Friends").Document(data_manager.accountID).Collection("friend").Document(accountID);
        Dictionary<string, object> other_data = new Dictionary<string, object>
        {
            { "accountID", accountID }
        };
        batch.Set(myRef, other_data);

        DocumentReference other_Ref = firestore.Collection("Friends").Document(accountID).Collection("inviting_friend").Document(key);
        batch.Delete(other_Ref);

        DocumentReference my_Ref = firestore.Collection("Friends").Document(data_manager.accountID).Collection("invite_friend").Document(key);
        batch.Delete(my_Ref);

        batch.CommitAsync().ContinueWithOnMainThread(add_task =>
        {
            if (add_task.IsFaulted || add_task.IsCanceled)
            {
                callback(3);
            }
            else if (add_task.IsCompleted)
            {
                callback(1);
            }
        });
    }

    public void reject_invite_friend(string key, string accountID, Callback callback)
    {
        WriteBatch batch = firestore.StartBatch();

        DocumentReference other_Ref = firestore.Collection("Friends").Document(accountID).Collection("inviting_friend").Document(key);
        batch.Delete(other_Ref);

        DocumentReference my_Ref = firestore.Collection("Friends").Document(data_manager.accountID).Collection("invite_friend").Document(key);
        batch.Delete(my_Ref);

        batch.CommitAsync().ContinueWithOnMainThread(add_task =>
        {
            if (add_task.IsFaulted || add_task.IsCanceled)
            {
                callback(3);
            }
            else if (add_task.IsCompleted)
            {
                callback(1);
            }
        });
    }

    public void multi_accept_invite_friend(string accountID)
    {
        WriteBatch batch = firestore.StartBatch();

        DocumentReference otherRef = firestore.Collection("Friends").Document(accountID).Collection("friend").Document(data_manager.accountID);
        Dictionary<string, object> my_data = new Dictionary<string, object>
        {
            { "accountID", data_manager.accountID }
        };
        batch.Set(otherRef, my_data);

        DocumentReference myRef = firestore.Collection("Friends").Document(data_manager.accountID).Collection("friend").Document(accountID);
        Dictionary<string, object> other_data = new Dictionary<string, object>
        {
            { "accountID", accountID }
        };
        batch.Set(myRef, other_data);

        batch.CommitAsync();
    }
    #endregion

    #region SERVICE
    public void get_question(Queue<ServiceManager.Chat> chat_list, Callback callback)
    {
        firestore.Collection("ChatQuestion").Document(data_manager.accountID).Collection("chat_value").GetSnapshotAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled)
            {
                Debug.Log("task Error: " + task.Exception);
            }
            else
            {
                QuerySnapshot snapshots = task.Result;
                foreach (DocumentSnapshot document in snapshots)
                {
                    if (document.Exists)
                    {
                        Dictionary<string, object> pairs = document.ToDictionary();

                        byte player_index = Convert.ToByte(pairs["player"].ToString());
                        string msg = pairs["msg"].ToString();

                        chat_list.Enqueue(new ServiceManager.Chat(player_index, msg));

                        if (!DataManager.instance.chat_question_key_list.Contains(document.Id))
                        {
                            DataManager.instance.chat_question_key_list.Add(document.Id);

                            DataManager.instance.save_chat_question_data();

                            ServiceManager.instance.on_alarm_image();
                        }
                    }
                    ServiceManager.instance.set_question_chatting_page();
                }
                callback(1);
            }
        });
    }

    public void send_question(string msg, Callback callback)
    {
        Dictionary<string, object> data = new Dictionary<string, object>
        {
            { "msg", msg },
            { "player" , 0 },
            { "time", DateTime.Now }
        };
        firestore.Collection("ChatQuestion").Document(data_manager.accountID).Collection("chat_value").AddAsync(data).ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled)
            {
                callback(3);
            }
            else if (task.IsCompleted)
            {
                callback(1);
            }
        });
    }
    #endregion

    #region REPLAY
    public delegate void Otube(Callback callback);
    public Dictionary<string, Otube> otube_func_dic = new Dictionary<string, Otube>();

    //화튜브에 업로드되어 있는 기록들을 원하는 타입에 따라 정렬하여 로드한다.
    public void load_omoktube_data(string type, Callback callback)
    {
        if (otube_func_dic.Count == 0)
        {
            otube_func_dic.Add("Time", load_time_otube_data);
            otube_func_dic.Add("View", load_view_otube_data);
            otube_func_dic.Add("Country", load_country_otube_data);
            otube_func_dic.Add("My", load_my_otube_data);
        }

        try
        {
            otube_func_dic[type](callback);
        }
        catch (Exception e)
        {
            //오튜브 로드 중 에러가 생긴 경우 3을 반환한다.
            Debug.Log("load_omoktube_data error => " + e);
            callback(3);
        }
    }

    public void load_time_otube_data(Callback callback)
    {
        Firebase.Firestore.Query query = firestore.Collection("Omoktube").OrderByDescending("time").WhereGreaterThan("time", DateTime.Now.AddDays(-30)).Limit(50);
        query.GetSnapshotAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled)
            {
                Debug.Log("task Error: " + task.Exception);
                callback(3);
            }
            else
            {
                foreach (DocumentSnapshot snapshot in task.Result.Documents)
                {
                    if (snapshot.Exists)
                    {
                        Dictionary<string, object> city = snapshot.ToDictionary();

                        string key = snapshot.Id;
                        string title = city["title"].ToString();
                        string score = city["score"].ToString();
                        string mode = city["mode"].ToString();
                        string time = ((Timestamp)city["time"]).ToDateTime().ToString();
                        string user_account_id = city["user_id"].ToString();
                        string user_name = city["user_name"].ToString();
                        COUNTRY user_country = Converter.to_country(city["user_country"].ToString());
                        TIER user_tier = Converter.to_tier(city["user_tier"].ToString());
                        int view = Convert.ToInt32(city["view"]);

                        ReplayManager.instance.omoktube_play_data_list.Add(new ReplayManager.OmoktubeRecordData(key, title, time, score, mode, user_account_id, user_name, user_country, user_tier, view));
                    }
                }
                callback(1);
            }
        });
    }

    public void load_view_otube_data(Callback callback)
    {
        Firebase.Firestore.Query query = firestore.Collection("Omoktube").OrderByDescending("view").OrderByDescending("time").Limit(50);
        query.GetSnapshotAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled)
            {
                Debug.Log("task Error: " + task.Exception);
                callback(3);
            }
            else
            {
                foreach (DocumentSnapshot snapshot in task.Result.Documents)
                {
                    if (snapshot.Exists)
                    {
                        Dictionary<string, object> city = snapshot.ToDictionary();

                        string key = snapshot.Id;
                        string title = city["title"].ToString();
                        string score = city["score"].ToString();
                        string mode = city["mode"].ToString();
                        string time = ((Timestamp)city["time"]).ToDateTime().ToString();
                        string user_account_id = city["user_id"].ToString();
                        string user_name = city["user_name"].ToString();
                        COUNTRY user_country = Converter.to_country(city["user_country"].ToString());
                        TIER user_tier = Converter.to_tier(city["user_tier"].ToString());
                        int view = Convert.ToInt32(city["view"]);

                        ReplayManager.instance.omoktube_play_data_list.Add(new ReplayManager.OmoktubeRecordData(key, title, time, score, mode, user_account_id, user_name, user_country, user_tier, view));
                    }
                }
                callback(1);
            }
        });
    }

    public void load_country_otube_data(Callback callback)
    {
        Firebase.Firestore.Query query = firestore.Collection("Omoktube").WhereEqualTo("user_country", data_manager.my_country.ToString()).OrderByDescending("time").Limit(50);
        query.GetSnapshotAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled)
            {
                Debug.Log("task Error: " + task.Exception);
                callback(3);
            }
            else
            {
                foreach (DocumentSnapshot snapshot in task.Result.Documents)
                {
                    if (snapshot.Exists)
                    {
                        Dictionary<string, object> city = snapshot.ToDictionary();

                        string key = snapshot.Id;
                        string title = city["title"].ToString();
                        string score = city["score"].ToString();
                        string mode = city["mode"].ToString();
                        string time = ((Timestamp)city["time"]).ToDateTime().ToString();
                        string user_account_id = city["user_id"].ToString();
                        string user_name = city["user_name"].ToString();
                        COUNTRY user_country = Converter.to_country(city["user_country"].ToString());
                        TIER user_tier = Converter.to_tier(city["user_tier"].ToString());
                        int view = Convert.ToInt32(city["view"]);

                        ReplayManager.instance.omoktube_play_data_list.Add(new ReplayManager.OmoktubeRecordData(key, title, time, score, mode, user_account_id, user_name, user_country, user_tier, view));
                    }
                }
                callback(1);
            }
        });
    }

    public void load_my_otube_data(Callback callback)
    {
        Firebase.Firestore.Query query = firestore.Collection("Omoktube").WhereEqualTo("user", data_manager.accountID).OrderByDescending("time").Limit(50);
        query.GetSnapshotAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled)
            {
                Debug.Log("task Error: " + task.Exception);
                callback(3);
            }
            else
            {
                foreach (DocumentSnapshot snapshot in task.Result.Documents)
                {
                    if (snapshot.Exists)
                    {
                        Dictionary<string, object> city = snapshot.ToDictionary();

                        string key = snapshot.Id;
                        string title = city["title"].ToString();
                        string score = city["score"].ToString();
                        string mode = city["mode"].ToString();
                        string time = ((Timestamp)city["time"]).ToDateTime().ToString();
                        string user_account_id = city["user_id"].ToString();
                        string user_name = city["user_name"].ToString();
                        COUNTRY user_country = Converter.to_country(city["user_country"].ToString());
                        TIER user_tier = Converter.to_tier(city["user_tier"].ToString());
                        int view = Convert.ToInt32(city["view"]);

                        ReplayManager.instance.omoktube_play_data_list.Add(new ReplayManager.OmoktubeRecordData(key, title, time, score, mode, user_account_id, user_name, user_country, user_tier, view));
                    }
                }
                callback(1);
            }
        });
    }

    //친구가 업로드한 기록들을 가져온다.
    public IEnumerator load_friend_omoktube_data(List<string> friends_id_list)
    {
        bool end = false;

        Firebase.Firestore.Query query = firestore.Collection("Omoktube").WhereIn("user", friends_id_list).OrderByDescending("time");
        query.GetSnapshotAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled)
            {
                Debug.Log("task Error: " + task.Exception);
            }
            else
            {
                foreach (DocumentSnapshot snapshot in task.Result.Documents)
                {
                    if (ReplayManager.instance.omoktube_play_data_list.Count >= 50)
                    {
                        break;
                    }

                    Dictionary<string, object> city = snapshot.ToDictionary();

                    string key = snapshot.Id;
                    string title = city["title"].ToString();
                    string score = city["score"].ToString();
                    string mode = city["mode"].ToString();
                    string time = ((Timestamp)city["time"]).ToDateTime().ToString();
                    string user_account_id = city["user_id"].ToString();
                    string user_name = city["user_name"].ToString();
                    COUNTRY user_country = Converter.to_country(city["user_country"].ToString());
                    TIER user_tier = Converter.to_tier(city["user_tier"].ToString());
                    int view = Convert.ToInt32(city["view"]);

                    ReplayManager.instance.omoktube_play_data_list.Add(new ReplayManager.OmoktubeRecordData(key, title, time, score, mode, user_account_id, user_name, user_country, user_tier, view));
                }
            }
            end = true;
        });
        yield return new WaitUntil(() => end);
    }

    public void load_this_record_data(string key, Callback callback)
    {
        firestore.Collection("Omoktube").Document(key).Collection("PlayData").Document("Data").GetSnapshotAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled)
            {
                Debug.Log("task Error: " + task.Exception);
                callback(3);
            }
            else
            {
                DocumentSnapshot snapshot = task.Result;
                if (snapshot.Exists)
                {
                    Debug.Log("snapshot is Exists");
                    Dictionary<string, object> pair = snapshot.ToDictionary();
                    string play_record = pair["record"].ToString();
                    Recorder.instance.replay_record = Recorder.convert_to_game_record(play_record);
                    callback(1);
                }
                else
                {
                    Debug.Log("snapshot is Null");
                    callback(3);
                }
            }
        });
    }

    public void check_upload_recode(string key, Callback callback)
    {
        firestore.Collection("Omoktube").Document(key).GetSnapshotAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled)
            {
                Debug.Log("task Error: " + task.Exception);
                callback(3);
            }
            else
            {
                if (task.Result.Exists)
                {
                    callback(2);
                }
                else
                {
                    callback(1);
                }
            }
        });
    }

    public void upload_to_omoktube(string key, string title, string score, string mode, string gameRecord)
    {
        Dictionary<string, object> hwatube = new Dictionary<string, object>
        {
            { "title", title },
            { "score" , score },
            { "mode" , mode },
            { "user_id", data_manager.accountID },
            { "user_device", data_manager.noticeToken },
            { "user_name", data_manager.my_name },
            { "user_country", data_manager.my_country.ToString() },
            { "user_tier", data_manager.my_tier.ToString() },
            { "view", 0 },
            { "upload_day", DateTime.Now.ToString("yyyy-MM-dd") },
            { "time", FieldValue.ServerTimestamp }
        };
        firestore.Collection("Omoktube").Document(key).SetAsync(hwatube);

        Dictionary<string, object> play = new Dictionary<string, object>
        {
            { "record", gameRecord }
        };
        firestore.Collection("Omoktube").Document(key).Collection("PlayData").Document("Data").SetAsync(play);
    }
#endregion

#region HISTORY
    public void send_get_event_history(string key, string main, EVENT_ITEM reward, long reward_count, string reason)
    {
        Dictionary<string, object> reward_value = new Dictionary<string, object>
        {
            { "Main", main },
            { "Reward", reward.ToString() },
            { "RewardCount", reward_count },
            { "Reason", reason },
            { "Day", TimeStamp.GetDayTime() },
            { "TimeStamp", TimeStamp.GetUnixTimeStamp() }
        };
        firestore.Collection("EventHistory").Document(data_manager.accountID).Collection("GetEvent").Document(key).SetAsync(reward_value);
    }

    public void send_purchase_history(string purchase_key, string purchase_item, string purchase_result, string purchase_time)
    {
        string key = TimeStamp.GetUnixTimeStamp();

        Dictionary<string, object> purchase_value = new Dictionary<string, object>
        {
            { "PurchaseKey", purchase_key },
            { "PurchaseItem", purchase_item },
            { "PurchaseResult", purchase_result },
            { "PurchaseTime", purchase_time },
            { "Day", TimeStamp.GetDayTime() },
            { "TimeStamp", TimeStamp.GetUnixTimeStamp() }
        };
        firestore.Collection("PurchaseHistory").Document(data_manager.accountID).Collection("PurchaseItem").Document(key).SetAsync(purchase_value);
    }

    public void send_play_history(int win, int lose, int tie, long get_money, long before_money, string type)
    {
        Dictionary<string, object> purchase_value = new Dictionary<string, object>
        {
            { "PlayCount", win + lose + tie },
            { "WinCount", win },
            { "LoseCount", lose },
            { "TieCount", tie },
            { "PlayTime", TimeStamp.GetNowTime() },
            { "TimeStamp", TimeStamp.GetUnixTimeStamp() },
            { "PlayType", type }
        };
        firestore.Collection("PlayHistory").Document(data_manager.accountID).Collection(TimeStamp.GetDayTime()).AddAsync(purchase_value);
    }
#endregion

#region ONLINE/OFFLINE
    public void online()
    {
        if (data_manager.accountID == "")
        {
            return;
        }

        int score_val = 0;

        try
        {
            string score = MatchingManager.instance.matching_score.ToString() + ((int)data_manager.my_tier).ToString() +
                get_win_percent(data_manager.b_win_count + data_manager.w_win_count, data_manager.b_lose_count + data_manager.w_lose_count, data_manager.b_tie_count + data_manager.w_tie_count).ToString();

            score_val = Converter.to_int(score);
        }
        catch (Exception e)
        {
            Debug.Log("Get OnlineUser score error! " + e);
        }

        DocumentReference docRef = firestore.Collection("OnlineUser").Document(data_manager.accountID);
        Dictionary<string, object> user = new Dictionary<string, object>
        {
            { "accountID",  data_manager.accountID },
            { "name",  data_manager.my_name },
            { "tier",  (int)data_manager.my_tier },
            { "time",  TimeStamp.GetUnixTimeStamp() },
            { "status", "0" },
            { "score", score_val },
            { "version", "ver2" }//구버전, 신버전 매칭 구별 필드 생성
        };
        docRef.SetAsync(user); 
    }

    public int get_win_percent(int win, int lose, int tie)
    {
        int value = 0;
        if (win + lose + tie != 0)
        {
            float win_count = win;
            float play_count = win + lose + tie;
            float val = Convert.ToSingle(win_count / play_count * 100);
            Debug.Log("get_win_percent value: " + val);
            value = Convert.ToInt32(val);
        }

        return value;
    }

    public void offline()
    {
        if (data_manager.accountID == "")
        {
            return;
        }
        firestore.Collection("OnlineUser").Document(data_manager.accountID).DeleteAsync();
    }
#endregion

    public void clear_my_data()
    {
        Dictionary<string, object> data = new Dictionary<string, object>
        {
            { "BlackWin", data_manager.b_win_count },
            { "BlackLose", data_manager.b_lose_count },
            { "BlackTie", data_manager.b_tie_count },

            { "WhiteWin", data_manager.w_win_count },
            { "WhiteLose", data_manager.w_lose_count },
            { "WhiteTie", data_manager.w_tie_count },

            { "BlackMonthWin", data_manager.b_month_win_count },
            { "BlackMonthLose", data_manager.b_month_lose_count },
            { "BlackMonthTie", data_manager.b_month_tie_count },

            { "WhiteMonthWin", data_manager.w_month_win_count },
            { "WhiteMonthLose", data_manager.w_month_lose_count },
            { "WhiteMonthTie", data_manager.w_month_tie_count },

            { "MonthWin", data_manager.b_month_win_count + data_manager.w_month_win_count  },

            { "BlackDayWin", data_manager.b_day_win_count },
            { "BlackDayLose", data_manager.b_day_lose_count },
            { "BlackDayTie", data_manager.b_day_tie_count },

            { "WhiteDayWin", data_manager.w_day_win_count },
            { "WhiteDayLose", data_manager.w_day_lose_count },
            { "WhiteDayTie", data_manager.w_day_tie_count },

            { "DayWin", data_manager.b_day_win_count + data_manager.w_day_win_count  },

            { "Name", "" },
            { "Country", COUNTRY.NONE },
            { "Tier", TIER.GRADE_12TH },
            { "Old", OLD.NONE },
            { "Gender", GENDER.NONE }
        };
        firestore.Collection("Users").Document(data_manager.accountID).SetAsync(data, SetOptions.MergeAll);
    }

#region ANALYTICS
    public static void AnalyticsLog(string msg, string param, string param_val)
    {
        if (param == null || param == "" || param_val == null || param_val == "")
        {
            FirebaseAnalytics.LogEvent(msg);
        }
        else
        {
            FirebaseAnalytics.LogEvent(msg, param, param_val);
        }
    }
#endregion
}
  