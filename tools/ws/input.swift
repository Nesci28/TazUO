import CoreGraphics
import Foundation

guard CommandLine.arguments.count >= 3,
      let x = Double(CommandLine.arguments[1]),
      let y = Double(CommandLine.arguments[2]) else {
    fputs("usage: tazuo-input x y [left|right]\n", stderr)
    exit(2)
}

let right = CommandLine.arguments.dropFirst(3).first == "right"
let point = CGPoint(x: x, y: y)
let down: CGEventType = right ? .rightMouseDown : .leftMouseDown
let up: CGEventType = right ? .rightMouseUp : .leftMouseUp
let button: CGMouseButton = right ? .right : .left
CGEvent(mouseEventSource: nil, mouseType: down, mouseCursorPosition: point, mouseButton: button)?.post(tap: .cghidEventTap)
CGEvent(mouseEventSource: nil, mouseType: up, mouseCursorPosition: point, mouseButton: button)?.post(tap: .cghidEventTap)
