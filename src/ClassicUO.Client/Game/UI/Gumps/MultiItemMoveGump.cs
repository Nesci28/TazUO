using System;
using System.Collections.Concurrent;
using ClassicUO.Assets;
using ClassicUO.Configuration;
using ClassicUO.Game.Data;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.Managers;
using ClassicUO.Game.Managers.Structs;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Game.UI.MyraWindows;
using ClassicUO.Game.UI.MyraWindows.Widgets;
using ClassicUO.Network;
using ClassicUO.Utility;
using Myra.Graphics2D;
using Myra.Graphics2D.UI;

namespace ClassicUO.Game.UI.Gumps
{
    internal class MultiItemMoveGump : MyraControl
    {
        private const int WIDTH = 230;
        private const int HEIGHT = 210;
        private const int BUTTON_GAP = 6;
        private const int MIN_RETRY_DELAY = 250;

        public static int PreferredWidth => UIManager.GetGump<MultiItemMoveGump>()?.Width ?? WIDTH;
        public static int PreferredHeight => UIManager.GetGump<MultiItemMoveGump>()?.Height ?? HEIGHT;

        public static void ShowNextTo(Control anchor, int padding = -2)
        {
            int w = PreferredWidth;
            int screenH = ScaleHelper.LogicalWindowHeight;

            int x = anchor.X >= w + padding
                ? anchor.X - (w + padding)           // left of anchor
                : anchor.X + anchor.Width + padding; // right of anchor

            int y = Math.Max(0, Math.Min(anchor.Y, screenH - PreferredHeight));

            MultiItemMoveGump g = UIManager.GetGump<MultiItemMoveGump>();
            if (g == null || g.IsDisposed)
            {
                AddMultiItemMoveGumpToUI(x, y);
                g = UIManager.GetGump<MultiItemMoveGump>();
            }

            if (g != null && !g.IsDisposed)
            {
                w = g.Width;
                x = anchor.X >= w + padding
                    ? anchor.X - (w + padding)
                    : anchor.X + anchor.Width + padding;
                y = Math.Max(0, Math.Min(anchor.Y, screenH - g.Height));
                g.SetPosition(x, y);
                g.SetInScreen();
            }
        }

        // ===== Selection + queue =====
        public static readonly ConcurrentQueue<Item> MoveItems = new ConcurrentQueue<Item>();
        private static readonly ConcurrentDictionary<uint, byte> _selected = new ConcurrentDictionary<uint, byte>();
        private static int SelectedCount => _selected.Count;

        // ===== Processing state =====
        public static int ObjDelay = 1000;
        private static bool processing = false;
        private static ProcessType processType = ProcessType.None;
        private static uint _lastMoveTick;
        private static uint tradeId, containerId, mobileId;
        private static int groundX, groundY, groundZ;
        private static PendingMoveAttempt _pendingMove;

        // ===== UI =====
        private readonly World _world;
        private MyraLabel _header;
        private MyraInputBox _delayInput;

        public static bool IsSelected(uint serial) => _selected.ContainsKey(serial);

        public MultiItemMoveGump(int x, int y)
            : base(TazLang.Get("multimove_title", "Multi move"))
        {
            _world = World.Instance;
            ObjDelay = ProfileManager.CurrentProfile.MoveMultiObjectDelay;
            EventSink.ClilocMessageReceived += OnClilocMessageReceived;

            Build();
            SetPosition(x, y);
        }

        private void Build()
        {
            var layout = new VerticalStackPanel
            {
                MinWidth = WIDTH,
                Spacing = MyraStyle.STANDARD_SPACING,
                Padding = new Thickness(8)
            };

            _header = new MyraLabel(TextForHeader(), MyraLabel.TextStyle.H3)
            {
                HorizontalAlignment = HorizontalAlignment.Center
            };
            layout.Widgets.Add(_header);

            var delayRow = new Grid { HorizontalAlignment = HorizontalAlignment.Stretch };
            delayRow.ColumnsProportions.Add(Proportion.Fill);
            delayRow.ColumnsProportions.Add(Proportion.Auto);
            delayRow.RowsProportions.Add(Proportion.Auto);

            var delayLabel = new MyraLabel(
                TazLang.Get("multimove_objectdelay", "Object delay:"),
                MyraLabel.TextStyle.P
            ) { VerticalAlignment = VerticalAlignment.Center };
            delayRow.Widgets.Add(delayLabel);

            _delayInput = new MyraInputBox
            {
                Text = ObjDelay.ToString(),
                Width = 64,
                InputFilter = MyraInputBox.DigitInputFilter
            };
            Grid.SetColumn(_delayInput, 1);
            delayRow.Widgets.Add(_delayInput);

            _delayInput.TextChangedByUser += (s, e) =>
            {
                if (int.TryParse(_delayInput.Text, out int newDelay))
                {
                    newDelay = Math.Max(0, newDelay);
                    if (newDelay == ObjDelay) return;
                    ObjDelay = newDelay;
                    ProfileManager.CurrentProfile.MoveMultiObjectDelay = newDelay;
                }
            };
            _delayInput.LostFocus = () =>
            {
                string normalized = ObjDelay.ToString();
                if (_delayInput.Text != normalized)
                    _delayInput.Text = normalized;
            };
            layout.Widgets.Add(delayRow);

            layout.Widgets.Add(MyraCheckButton.CreateWithCallback(
                ProfileManager.CurrentProfile.MoveMultiAutoRetry,
                isChecked => ProfileManager.CurrentProfile.MoveMultiAutoRetry = isChecked,
                TazLang.Get("multimove_autoretry", "Auto retry"),
                TazLang.Get(
                    "multimove_autoretry_tooltip",
                    "Keep retrying unconfirmed item moves until they succeed or you cancel."
                )
            ));

            layout.Widgets.Add(new MyraButton(
                TazLang.Get("multimove_movetobackpack", "Move to backpack"),
                () =>
                {
                    Item backpack = _world.Player?.Backpack;
                    if (backpack != null)
                        ProcessItemMoves(_world, backpack);
                }
            )
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Tooltip = TazLang.Get(
                    "multimove_movetobackpack_tooltip",
                    "Move selected items to your backpack."
                )
            });

            var favoriteRow = CreateButtonRow();
            var setFavoriteButton = new MyraButton(
                TazLang.Get("multimove_setfavoritebag", "Set favorite bag"),
                () =>
                {
                    GameActions.Print(_world, TazLang.Get("multimove_targetfavorite", "Target a container to set as your favorite."));
                    _world.TargetManager.SetTargeting(CursorTarget.SetFavoriteMoveBag, CursorType.Target, TargetType.Neutral);
                }
            )
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Tooltip = TazLang.Get(
                    "multimove_setfavoritebag_tooltip",
                    "Set your preferred destination container for future item moves."
                )
            };
            favoriteRow.Widgets.Add(setFavoriteButton);

            var toFavoriteButton = new MyraButton(
                TazLang.Get("multimove_tofavorite", "To favorite"),
                () =>
                {
                    uint fav = ProfileManager.CurrentProfile.SetFavoriteMoveBagSerial;
                    if (fav == 0)
                    {
                        GameActions.Print(_world, TazLang.Get("multimove_nofavorite", "No favorite container set. Please target one."));
                        _world.TargetManager.SetTargeting(CursorTarget.SetFavoriteMoveBag, CursorType.Target, TargetType.Neutral);
                        return;
                    }

                    Item cont = _world.Items.Get(fav);
                    if (cont != null)
                        ProcessItemMoves(_world, cont);
                    else
                        GameActions.Print(_world, TazLang.Get("multimove_favoriteunavailable", "Favorite container is not available."));
                }
            )
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Tooltip = TazLang.Get(
                    "multimove_tofavorite_tooltip",
                    "Move selected items to your favorite container."
                )
            };
            Grid.SetColumn(toFavoriteButton, 1);
            favoriteRow.Widgets.Add(toFavoriteButton);
            layout.Widgets.Add(favoriteRow);

            var actionRow = CreateButtonRow();
            var cancelButton = new MyraButton(
                TazLang.Get("multimove_cancel", "Cancel"),
                Dispose
            ) { HorizontalAlignment = HorizontalAlignment.Stretch };
            actionRow.Widgets.Add(cancelButton);

            var moveToButton = new MyraButton(
                TazLang.Get("multimove_moveto", "Move to"),
                () =>
                {
                    GameActions.Print(_world, TazLang.Get("multimove_wheremove", "Where should we move these items?"));
                    _world.TargetManager.SetTargeting(CursorTarget.MoveItemContainer, CursorType.Target, TargetType.Neutral);
                }
            )
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Tooltip = TazLang.Get(
                    "multimove_moveto_tooltip",
                    "Select a container, a mobile (or its name plate), or a ground tile to move these items to."
                )
            };
            Grid.SetColumn(moveToButton, 1);
            actionRow.Widgets.Add(moveToButton);
            layout.Widgets.Add(actionRow);

            SetRootContent(layout);
        }

        private static Grid CreateButtonRow()
        {
            var row = new Grid
            {
                ColumnSpacing = BUTTON_GAP,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            row.ColumnsProportions.Add(new Proportion(ProportionType.Part, 1));
            row.ColumnsProportions.Add(new Proportion(ProportionType.Part, 1));
            row.RowsProportions.Add(Proportion.Auto);
            return row;
        }

        // ===== Selection API used by GridContainer =====

        public static bool TrySelect(Item item)
        {
            if (item == null) return false;
            if (!_selected.TryAdd(item.Serial, 1)) return false; // already selected
            MoveItems.Enqueue(item);
            return true;
        }

        /// <summary>
        /// Toggle selection state of an item. Returns true if now selected; false if deselected.
        /// </summary>
        public static bool ToggleItem(Item item)
        {
            if (item == null) return false;

            if (_selected.TryRemove(item.Serial, out _))
            {
                // deselected
                CancelPendingMove(item.Serial);
                return false;
            }

            _selected[item.Serial] = 1;
            MoveItems.Enqueue(item);
            return true;
        }

        public static void AddMultiItemMoveGumpToUI(int x, int y)
        {
            if (SelectedCount > 0)
            {
                MultiItemMoveGump g = UIManager.GetGump<MultiItemMoveGump>();
                if (g == null || g.IsDisposed)
                    UIManager.Add(new MultiItemMoveGump(x, y));
            }
        }

        // ===== Target entry points =====

        public static void OnContainerTarget(World world, uint serial)
        {
            if (SerialHelper.IsMobile(serial))
            {
                Mobile mobile = world.Mobiles.Get(serial);
                if (mobile == null)
                {
                    GameActions.Print(world, TazLang.Get("multimove_invalidmobile", "That does not appear to be a valid mobile..."));
                    return;
                }
                GameActions.Print(world, TazLang.Get("multimove_movingtomobile", "Moving items to the selected mobile.."));
                ProcessItemMovesToMobile(world, mobile.Serial);
                return;
            }

            if (SerialHelper.IsItem(serial))
            {
                Item moveToContainer = world.Items.Get(serial);
                if (moveToContainer == null || !moveToContainer.ItemData.IsContainer)
                {
                    GameActions.Print(world, TazLang.Get("multimove_notcontainer", "That does not appear to be a container..."));
                    return;
                }
                GameActions.Print(world, TazLang.Get("multimove_movingtocontainer", "Moving items to the selected container.."));
                ProcessItemMoves(world, moveToContainer);
            }
        }

        public static void OnContainerTarget(World world, int x, int y, int z) => ProcessItemMoves(world, x, y, z);


        public static void OnTradeWindowTarget(World world, uint tradeID) => ProcessItemMoves(world, tradeID);

        // ===== Processing impl =====

        private static void ProcessItemMoves(World world, Item container)
        {
            if (container != null)
            {
                RequeuePendingMove();
                containerId = container.Serial;
                processType = ProcessType.Container;
                processing = true;
            }
        }

        private static void ProcessItemMoves(World world, int x, int y, int z)
        {
            RequeuePendingMove();
            processType = ProcessType.Ground;
            groundX = x;
            groundY = y;
            groundZ = z;
            processing = true;
        }

        private static void ProcessItemMoves(World world, uint tradeID)
        {
            RequeuePendingMove();
            tradeId = tradeID;
            processType = ProcessType.TradeWindow;
            processing = true;
        }

        private static void ProcessItemMovesToMobile(World world, uint mobileSerial)
        {
            RequeuePendingMove();
            mobileId = mobileSerial;
            processType = ProcessType.Mobile;
            processing = true;
        }

        public override void Update()
        {
            base.Update();

            if (IsDisposed)
            {
                ClearAll();
                return;
            }

            // live header
            if (_header != null)
                _header.Text = TextForHeader();

            if (SelectedCount == 0)
            {
                Dispose();
                return;
            }

            if (!processing)
                return;

            if (_pendingMove != null)
            {
                UpdatePendingMove(_world);
                FinishProcessingIfComplete();
                return;
            }

            // Respect object delay with overflow-safe delta check
            if (Time.Ticks - _lastMoveTick < (uint)ObjDelay)
                 return;

            if (Client.Game.UO.GameCursor.ItemHold.Enabled)
                return;

            if (MoveItems.TryDequeue(out Item moveItem))
            {
                if (_selected.ContainsKey(moveItem.Serial))
                {
                    Item currentItem = _world.Items.Get(moveItem.Serial);

                    if (HasReachedDestination(_world, currentItem))
                    {
                        CompleteMove(moveItem.Serial);
                    }
                    else if (TryCreateMoveRequest(currentItem, out MoveRequest request))
                    {
                        if (ProfileManager.CurrentProfile.MoveMultiAutoRetry)
                            EnqueueRetryableMove(currentItem, request);
                        else
                        {
                            ObjectActionQueue.Instance.Enqueue(
                                request.ToObjectActionQueueItem(),
                                ActionPriority.MoveItem
                            );
                            CompleteMove(moveItem.Serial);
                        }

                        _lastMoveTick = Time.Ticks;
                    }
                }
                // else: was deselected after enqueue -> skip
            }

            FinishProcessingIfComplete();
        }

        public override void Dispose()
        {
            EventSink.ClilocMessageReceived -= OnClilocMessageReceived;
            ClearAll();
            base.Dispose();
        }

        private static string TextForHeader()
        {
            int count = SelectedCount;
            return processing
                ? TazLang.Get("multimove_header_moving", new[] { count.ToString() })
                : TazLang.Get("multimove_header_selected", new[] { count.ToString() });
        }

        private static void ClearAll()
        {
            CancelPendingMove();
            _selected.Clear();
            while (MoveItems.TryDequeue(out _)) { }
            processing = false;
            ResetDestination();
        }

        private static void ResetDestination()
        {
            processType = ProcessType.None;
            containerId = 0;
            mobileId = 0;
            tradeId = 0;
            groundX = groundY = groundZ = 0;
        }

        private static bool TryCreateMoveRequest(Item item, out MoveRequest request)
        {
            request = default;

            if (item == null)
                return false;

            switch (processType)
            {
                case ProcessType.Ground:
                    StaticTiles itemData = Client.Game.UO.FileManager.TileData.StaticData[item.Graphic];
                    request = new MoveRequest(
                        item.Serial,
                        0,
                        item.Amount,
                        groundX,
                        groundY,
                        groundZ + (sbyte)(itemData.Height == 0xFF ? 0 : itemData.Height)
                    );
                    return true;

                case ProcessType.Container:
                    request = new MoveRequest(item.Serial, containerId, item.Amount);
                    return true;

                case ProcessType.Mobile:
                    request = new MoveRequest(item.Serial, mobileId, item.Amount);
                    return true;

                case ProcessType.TradeWindow:
                    request = new MoveRequest(
                        item.Serial,
                        tradeId,
                        item.Amount,
                        RandomHelper.GetValue(0, 20),
                        RandomHelper.GetValue(0, 20)
                    );
                    return true;

                case ProcessType.None:
                default:
                    processing = false;
                    ResetDestination();
                    return false;
            }
        }

        private static void EnqueueRetryableMove(Item item, MoveRequest request)
        {
            PendingMoveAttempt pending = new PendingMoveAttempt(item);
            ObjectActionQueueItem queueItem = new ObjectActionQueueItem(
                request.Execute,
                action =>
                {
                    if (!action.Canceled)
                    {
                        pending.WasInvoked = true;
                        pending.InvokedAt = Time.Ticks;
                    }
                }
            );

            pending.QueueItem = queueItem;
            _pendingMove = pending;
            ObjectActionQueue.Instance.Enqueue(queueItem, ActionPriority.MoveItem);
        }

        private void OnClilocMessageReceived(object sender, MessageEventArgs e)
        {
            if (
                e.Cliloc != 500119
                || !processing
                || !ProfileManager.CurrentProfile.MoveMultiAutoRetry
            )
            {
                return;
            }

            PendingMoveAttempt pending = _pendingMove;

            if (pending == null || !pending.WasInvoked)
                return;

            Item currentItem = _world.Items.Get(pending.Serial);

            if (!pending.IsStillAtSource(currentItem))
                return;

            _pendingMove = null;
            MoveItems.Enqueue(currentItem);

            long retryDelay = (AsyncNetClient.Socket?.Statistics?.Ping ?? 0)
                              + GlobalActionCooldown.NetworkSafetyMargin;
            GlobalActionCooldown.BeginCooldown(retryDelay);

            _lastMoveTick = unchecked(Time.Ticks - (uint)Math.Max(ObjDelay, 0));
        }

        private static void UpdatePendingMove(World world)
        {
            PendingMoveAttempt pending = _pendingMove;

            if (!_selected.ContainsKey(pending.Serial))
            {
                CancelPendingMove();
                return;
            }

            if (!pending.WasInvoked)
                return;

            if (!ProfileManager.CurrentProfile.MoveMultiAutoRetry)
            {
                CompleteMove(pending.Serial);
                return;
            }

            Item currentItem = world.Items.Get(pending.Serial);

            if (HasReachedDestination(world, currentItem))
            {
                CompleteMove(pending.Serial);
                return;
            }

            uint retryDelay = (uint)Math.Max(ObjDelay, MIN_RETRY_DELAY);

            if (Time.Ticks - pending.InvokedAt >= retryDelay)
            {
                if (pending.IsStillAtSource(currentItem))
                {
                    _pendingMove = null;
                    MoveItems.Enqueue(currentItem);
                }
                else
                    CompleteMove(pending.Serial);
            }
        }

        private static bool HasReachedDestination(World world, Item item)
        {
            if (item == null || item.IsDestroyed)
                return true;

            switch (processType)
            {
                case ProcessType.Container:
                    return item.Container == containerId;

                case ProcessType.Mobile:
                    Item tradeBox = world.Player?.GetSecureTradeBox();
                    return item.Container == mobileId || tradeBox != null && item.Container == tradeBox.Serial;

                case ProcessType.TradeWindow:
                    return item.Container == tradeId;

                case ProcessType.Ground:
                    return item.OnGround && item.X == groundX && item.Y == groundY;

                default:
                    return false;
            }
        }

        private static void CompleteMove(uint serial)
        {
            if (_pendingMove?.Serial == serial)
                _pendingMove = null;

            _selected.TryRemove(serial, out _);
        }

        private static void RequeuePendingMove()
        {
            PendingMoveAttempt pending = _pendingMove;

            if (pending == null)
                return;

            if (!pending.WasInvoked)
                pending.QueueItem.SetCanceled();

            _pendingMove = null;

            if (_selected.ContainsKey(pending.Serial))
                MoveItems.Enqueue(pending.Item);
        }

        private static void CancelPendingMove(uint serial = 0)
        {
            PendingMoveAttempt pending = _pendingMove;

            if (pending == null || serial != 0 && pending.Serial != serial)
                return;

            if (!pending.WasInvoked)
                pending.QueueItem.SetCanceled();

            _pendingMove = null;
        }

        private static void FinishProcessingIfComplete()
        {
            if (_pendingMove == null && MoveItems.IsEmpty && SelectedCount == 0)
            {
                processing = false;
                ResetDestination();
            }
        }

        private sealed class PendingMoveAttempt
        {
            public PendingMoveAttempt(Item item)
            {
                Item = item;
                Serial = item.Serial;
                SourceContainer = item.Container;
                SourceOnGround = item.OnGround;
                SourceX = item.X;
                SourceY = item.Y;
                SourceZ = item.Z;
            }

            public Item Item { get; }
            public uint Serial { get; }
            private uint SourceContainer { get; }
            private bool SourceOnGround { get; }
            private ushort SourceX { get; }
            private ushort SourceY { get; }
            private sbyte SourceZ { get; }
            public ObjectActionQueueItem QueueItem { get; set; }
            public bool WasInvoked { get; set; }
            public uint InvokedAt { get; set; }

            public bool IsStillAtSource(Item item)
            {
                if (item == null || item.IsDestroyed || item.Container != SourceContainer)
                    return false;

                return !SourceOnGround || item.OnGround && item.X == SourceX && item.Y == SourceY && item.Z == SourceZ;
            }
        }

        protected enum ProcessType
        {
            None = 0,
            Container,
            Ground,
            TradeWindow,
            Mobile
        }
    }
}
