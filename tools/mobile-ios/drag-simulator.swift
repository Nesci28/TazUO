import CoreGraphics
import Foundation

// Host-screen coordinates in points, as reported by macOS Accessibility.
// The target must be the foreground Simulator window.
guard CommandLine.arguments.count == 6,
      let x1 = Double(CommandLine.arguments[1]),
      let y1 = Double(CommandLine.arguments[2]),
      let x2 = Double(CommandLine.arguments[3]),
      let y2 = Double(CommandLine.arguments[4]),
      let duration = Double(CommandLine.arguments[5]),
      duration > 0, duration <= 5 else {
    fputs("usage: drag-simulator startX startY endX endY holdSeconds (0..5)\n", stderr)
    exit(2)
}

func send(_ type: CGEventType, _ point: CGPoint) {
    CGEvent(mouseEventSource: nil, mouseType: type, mouseCursorPosition: point,
            mouseButton: .left)?.post(tap: .cghidEventTap)
}
let start = CGPoint(x: x1, y: y1)
let end = CGPoint(x: x2, y: y2)
send(.mouseMoved, start)
send(.leftMouseDown, start)
defer { send(.leftMouseUp, end) }
for step in 1...10 {
    let fraction = Double(step) / 10
    send(.leftMouseDragged, CGPoint(x: x1 + (x2 - x1) * fraction,
                                   y: y1 + (y2 - y1) * fraction))
    Thread.sleep(forTimeInterval: 0.02)
}
Thread.sleep(forTimeInterval: duration)
