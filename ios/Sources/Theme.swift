import SwiftUI

/// 和 Windows 版同一套视觉语言：深空蓝底 + 青蓝主色 + 暖橙点缀。
enum Theme {
    static let bg0 = Color(red: 0.043, green: 0.059, blue: 0.078)
    static let bg1 = Color(red: 0.075, green: 0.098, blue: 0.129)
    static let bg2 = Color(red: 0.106, green: 0.141, blue: 0.184)
    static let bg3 = Color(red: 0.145, green: 0.192, blue: 0.247)

    static let textHi = Color(red: 0.914, green: 0.945, blue: 0.980)
    static let textMid = Color(red: 0.580, green: 0.647, blue: 0.725)
    static let textLow = Color(red: 0.357, green: 0.420, blue: 0.494)

    static let cyan = Color(red: 0.227, green: 0.847, blue: 0.914)
    static let cyanDeep = Color(red: 0.094, green: 0.576, blue: 0.659)
    static let amber = Color(red: 1.0, green: 0.690, blue: 0.361)
    static let rose = Color(red: 1.0, green: 0.420, blue: 0.506)

    static var background: LinearGradient {
        LinearGradient(
            colors: [
                Color(red: 0.086, green: 0.114, blue: 0.153),
                Color(red: 0.047, green: 0.067, blue: 0.094),
                Color(red: 0.031, green: 0.043, blue: 0.063)
            ],
            startPoint: .top,
            endPoint: .bottom
        )
    }
}

/// 从曲目信息哈希出一对颜色，每首歌的唱片都不一样。
func discColors(for key: String) -> (Color, Color) {
    var hash: UInt64 = 17
    for scalar in key.unicodeScalars {
        hash = hash &* 31 &+ UInt64(scalar.value)
    }
    // 178..256 度：青 → 蓝，和主题同族
    let hue = 178.0 + Double(hash % 78)
    return (hsv(hue, 0.62, 0.95), hsv((hue + 42).truncatingRemainder(dividingBy: 360), 0.78, 0.55))
}

private func hsv(_ h: Double, _ s: Double, _ v: Double) -> Color {
    let hi = Int(h / 60) % 6
    let f = h / 60 - (h / 60).rounded(.down)
    let p = v * (1 - s)
    let q = v * (1 - f * s)
    let t = v * (1 - (1 - f) * s)

    let r: Double, g: Double, b: Double
    switch hi {
    case 0: (r, g, b) = (v, t, p)
    case 1: (r, g, b) = (q, v, p)
    case 2: (r, g, b) = (p, v, t)
    case 3: (r, g, b) = (p, q, v)
    case 4: (r, g, b) = (t, p, v)
    default: (r, g, b) = (v, p, q)
    }
    return Color(red: r, green: g, blue: b)
}

func formatTime(_ seconds: Double) -> String {
    guard seconds.isFinite, seconds >= 0 else { return "00:00" }
    let total = Int(seconds.rounded(.down))
    let m = total / 60
    let s = total % 60
    return String(format: "%02d:%02d", m, s)
}
