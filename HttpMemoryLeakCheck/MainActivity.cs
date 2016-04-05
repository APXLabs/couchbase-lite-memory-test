using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Android.App;
using Android.Widget;
using Android.OS;
using Android.Util;
using Couchbase.Lite;
using Couchbase.Lite.Auth;
using Couchbase.Lite.Replicator;

namespace HttpMemoryLeakCheck
{
    [Activity(Label = "HttpMemoryLeakCheck", MainLauncher = true, Icon = "@drawable/icon")]
    public class MainActivity : Activity
    {
        private const string Tag = "ELC";
        private const string Scheme = "http";
        private const string Host = "10.2.1.125";
        private const int Port = 4984;
        private const string DbName = "couchbaseevents";
        private const string User = "couchbase_user";
        private const string Password = "mobile";

        private int _count;
        private Database _db;
        private Replication _pull;
        private Replication _push;
        private Button _button;
        private TextView _numberGenerated;
        private TableLayout _layout;
        private bool _shouldBeStopped = true;
        private readonly object _insertLock = new object();

        protected override void OnCreate(Bundle bundle)
        {
            base.OnCreate(bundle);
            try
            {
                Task.Factory.StartNew(() =>
                {
                    StartDb();
                    StartReplications();
                });

                // Set our view from the "main" layout resource
                SetContentView(Resource.Layout.Main);

                // Get our button from the layout resource,
                // and attach an event to it
                _button = FindViewById<Button>(Resource.Id.MyButton);

                _button.Click += OnClicked;

                lock (_insertLock)
                {
                    _layout = FindViewById<TableLayout>(Resource.Id.tableLayout1);
                }

                _numberGenerated = FindViewById<TextView>(Resource.Id.textViewDocGen);
                _numberGenerated.Text = $"Documents generated: {_count}";
            }
            catch (Exception exception)
            {
                Log.Debug(Tag, $"Exception caught: {exception.Message}");
            }
        }

        private void OnClicked(object sender, EventArgs eventArgs)
        {
            _shouldBeStopped = !_shouldBeStopped;
            var state = _shouldBeStopped ? "Start" : "Stop";

            _button.Text = $"Tap to {state}";

            if (!_shouldBeStopped)
                _push.Start();
        }

        private string AddDoc()
        {
            var doc = _db.CreateDocument();
            string docId = doc.Id;
            var props = new Dictionary<string, object>
            {
                {"name", "Big Party"},
                {"location", "MyHouse"},
                {"date", DateTime.Now}
            };
            try
            {
                doc.PutProperties(props);
                Log.Debug(Tag, $"doc written to database with ID = {doc.Id}");
            }
            catch (Exception e)
            {
                Log.Error(Tag, "Error putting properties to Couchbase Lite database", e);
            }
            return docId;
        }

        private void AddAttachment(string id)
        {
            var doc = _db.GetDocument(id);
            var data = MakeData(19);
            var rev = doc.CreateRevision();
            rev.SetAttachment(Guid.NewGuid().ToString(), "application/octet-stream", data);
            rev.Save(false);
            ++_count;
            Log.Debug(Tag, $"attachment {_count} added to database on doc {doc.Id}");
        }


        private void StartDb()
        {
            _db = Manager.SharedInstance.GetDatabase("test");
        }

        private static Uri CreateSyncUri()
        {
            Uri syncUri = null;
            try
            {
                var uriBuilder = new UriBuilder(Scheme, Host, Port, DbName);
                syncUri = uriBuilder.Uri;
            }
            catch (UriFormatException e)
            {
                Log.Error(Tag, "Cannot create sync uri", e);
            }
            return syncUri;
        }

        private void StartReplications()
        {
            _pull = _db.CreatePullReplication(CreateSyncUri());
            _push = _db.CreatePushReplication(CreateSyncUri());
            var authenticator = AuthenticatorFactory.CreateBasicAuthenticator(User, Password);
            _pull.Authenticator = authenticator;
            _push.Authenticator = authenticator;
            _pull.Continuous = true;
            _push.Continuous = false;
            _pull.Start();
           // _push.Start();
            _push.Changed += OnPushChanged;
        }

        private void OnPushChanged(object sender, ReplicationChangeEventArgs replicationChangeEventArgs)
        {
            switch (replicationChangeEventArgs.ReplicationStateTransition.Destination)
            {
                case ReplicationState.Stopped:
                    if (!_shouldBeStopped)
                    {
                        var id = AddDoc();
                        AddAttachment(id);
                        _push.Start();
                    }
                    break;
            }

            RunOnUiThread(
                () =>
                    InsertInfo(replicationChangeEventArgs.ReplicationStateTransition.Destination.ToString(),
                        DateTime.Now.ToString(CultureInfo.CurrentCulture)));
            RunOnUiThread(() => _numberGenerated.Text = $"Documents generated: {_count}");
        }

        private void InsertInfo(string status, string time)
        {
            var row = CreateTableRow(status, time);
            if (row == null)
                return;
            lock (_insertLock)
            {
                _layout.AddView(row, 1);
            }
        }

        private static TableRow CreateTableRow(string status, string time)
        {
            var tv = new TextView(Application.Context)
            {
                TextSize = 14,
                Text = status
            };

            var tv2 = new TextView(Application.Context)
            {
                TextSize = 14,
                Text = time
            };

            var tr = new TableRow(Application.Context);
            tr.AddView(tv);
            tr.AddView(tv2);
            return tr;
        }

        /// <summary>
        /// Creates a data blob
        /// </summary>
        /// <param name="dataSize">size in MB</param>
        /// <returns></returns>
        private static byte[] MakeData(long dataSize)
        {
            var data = new byte[dataSize*1024*1024];
            return data;
        }
    }
}