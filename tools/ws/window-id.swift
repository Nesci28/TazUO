import CoreGraphics
import Foundation

let windows = CGWindowListCopyWindowInfo([.optionOnScreenOnly, .excludeDesktopElements], kCGNullWindowID) as? [[String: Any]] ?? []
let candidates = windows.compactMap { window -> (Int, Int)? in
    guard let owner = window[kCGWindowOwnerName as String] as? String,
          owner.localizedCaseInsensitiveContains("TazUO"),
          let id = window[kCGWindowNumber as String] as? Int,
          let bounds = window[kCGWindowBounds as String] as? [String: Any],
          let widthNumber = bounds["Width"] as? NSNumber else { return nil }
    let width = widthNumber.intValue
    guard width > 1000 else { return nil }
    return (id, width)
}
if let selected = candidates.max(by: { $0.1 < $1.1 }) { print(selected.0) }
