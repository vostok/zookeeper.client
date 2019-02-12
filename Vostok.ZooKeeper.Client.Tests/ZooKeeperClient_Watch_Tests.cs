namespace Vostok.ZooKeeper.Client.Tests
{
    public class ZooKeeperClient_Watch_Tests
    {
//        private ZooKeeperClient client;

//        private int notificationsReceived;
//        private IWatcher countingWatcher;
//        private string rootNode;
//        private string childNode;

//        public AnonymousWatcher_Tests()
//            : base(new SilentLog())
//        {
//        }

//        [SetUp]
//        public void SetUp()
//        {
//            client = CreateNewClient();

//            notificationsReceived = 0;
//            countingWatcher = new AnonymousWatcher((type, s) => Interlocked.Increment(ref notificationsReceived));
//            rootNode = "/" + Guid.NewGuid();
//            childNode = rootNode + "/child";
//        }

//        [TearDown]
//        public void TearDown()
//        {
//            client.Delete(rootNode);
//            client.Dispose();
//        }

//        [Test]
//        public void Should_not_produce_duplicate_notifications_for_same_watcher_with_get_children_operation()
//        {
//            CreateNode(rootNode, CreateMode.Persistent, client);

//            for (var i = 0; i < 10; i++)
//                client.GetChildren(rootNode, countingWatcher);

//            CreateNode(childNode, CreateMode.Persistent, client);

//            Action checkNotificationsCountAction = () => notificationsReceived.Should().Be(1);
//            checkNotificationsCountAction.ShouldPassIn(5.Seconds());
//        }

//        [Test]
//        public void Should_not_produce_duplicate_notifications_for_same_watcher_with_exists_operation()
//        {
//            CreateNode(rootNode, CreateMode.Persistent, client);

//            for (var i = 0; i < 10; i++)
//                client.Exists(rootNode, countingWatcher);

//            DeleteNode(rootNode, client);

//            Action checkNotificationsCountAction = () => notificationsReceived.Should().Be(1);
//            checkNotificationsCountAction.ShouldPassIn(5.Seconds());
//        }

//        [Test]
//        public void Should_allow_same_watcher_to_receive_multiple_notifications_from_different_get_children_operations()
//        {
//            CreateNode(rootNode, CreateMode.Persistent, client);

//            for (var i = 1; i <= 3; i++)
//            {
//                client.GetChildren(rootNode, countingWatcher);
//                CreateNode(childNode + i, CreateMode.Ephemeral, client);
//            }

//            Action checkNotificationsCountAction = () => notificationsReceived.Should().Be(3);
//            checkNotificationsCountAction.ShouldPassIn(5.Seconds());
//        }

//        [Test]
//        public void Should_allow_same_watcher_to_receive_multiple_notifications_from_different_exists_operations()
//        {
//            for (var i = 1; i <= 3; i++)
//            {
//                CreateNode(rootNode, CreateMode.Persistent, client);
//                client.Exists(rootNode, countingWatcher);
//                DeleteNode(rootNode, client);
//            }

//            Action checkNotificationsCountAction = () => notificationsReceived.Should().Be(3);
//            checkNotificationsCountAction.ShouldPassIn(5.Seconds());
//        }
    }
}