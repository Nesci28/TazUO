#import <Foundation/Foundation.h>
#import <UIKit/UIKit.h>
#import <WebKit/WebKit.h>

extern "C" void UnitySendMessage(const char *obj, const char *method, const char *msg);

static WKWebView *TazUOLegionWebView;
static NSString *TazUOLegionReceiver;

@interface TazUOLegionMessageHandler : NSObject<WKScriptMessageHandler>
@end

@implementation TazUOLegionMessageHandler
- (void)userContentController:(WKUserContentController *)controller
      didReceiveScriptMessage:(WKScriptMessage *)message {
    if (![message.body isKindOfClass:[NSString class]] || TazUOLegionReceiver == nil)
        return;
    UnitySendMessage(TazUOLegionReceiver.UTF8String, "OnLegionMessage", [message.body UTF8String]);
}
@end

static TazUOLegionMessageHandler *TazUOLegionHandler;

extern "C" void TazUO_LegionStart(const char *htmlPath, const char *receiver) {
    if (htmlPath == nullptr || htmlPath[0] == '\0')
        return;
    // P/Invoke owns the C buffers only for the duration of this call.
    NSString *path = [NSString stringWithUTF8String:htmlPath];
    NSString *receiverName = [NSString stringWithUTF8String:receiver ?: "TazUO Legion Host"];
    if (path.length == 0)
        return;
    dispatch_async(dispatch_get_main_queue(), ^{
        UIWindow *window = nil;
        for (UIScene *scene in UIApplication.sharedApplication.connectedScenes) {
            if (![scene isKindOfClass:[UIWindowScene class]])
                continue;
            for (UIWindow *candidate in ((UIWindowScene *)scene).windows) {
                if (candidate.isKeyWindow) {
                    window = candidate;
                    break;
                }
            }
            if (window != nil)
                break;
        }
        UIViewController *controller = window.rootViewController;
        if (controller == nil)
            return;
        TazUOLegionReceiver = receiverName;
        TazUOLegionHandler = [TazUOLegionMessageHandler new];
        WKUserContentController *content = [WKUserContentController new];
        [content addScriptMessageHandler:TazUOLegionHandler name:@"unity"];
        WKWebViewConfiguration *configuration = [WKWebViewConfiguration new];
        configuration.userContentController = content;
        TazUOLegionWebView = [[WKWebView alloc] initWithFrame:controller.view.bounds configuration:configuration];
        TazUOLegionWebView.hidden = YES;
        [controller.view addSubview:TazUOLegionWebView];
        NSURL *url = [NSURL fileURLWithPath:path];
        [TazUOLegionWebView loadFileURL:url allowingReadAccessToURL:url.URLByDeletingLastPathComponent];
    });
}

extern "C" void TazUO_LegionSend(const char *javascript) {
    if (TazUOLegionWebView == nil || javascript == nullptr)
        return;
    NSString *source = [NSString stringWithUTF8String:javascript];
    dispatch_async(dispatch_get_main_queue(), ^{
        [TazUOLegionWebView evaluateJavaScript:source completionHandler:nil];
    });
}
