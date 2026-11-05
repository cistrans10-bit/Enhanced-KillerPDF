//! Drawings laid out the way Word lays them out (diagrams in reports: shapes, connectors, text
//! boxes and canvases around and between text).

use super::*;
use wordcraft_doc::Pos;
use wordcraft_doc::connector::{Bent, ShapeExtra};
use wordcraft_doc::para::{FloatAlign, ShapeKind};

fn lay(doc: &Document) -> DocLayout {
    layout(doc, &mut LayoutCache::new(), &LayoutOptions::default())
}

fn shape(kind: ShapeKind, w: f32, h: f32, float: Float, extra: ShapeExtra) -> InlineObject {
    InlineObject::Shape {
        kind,
        w,
        h,
        fill: Some(Rgb(255, 255, 255)),
        stroke: Some(Rgb(0, 0, 0)),
        stroke_width: 1.0,
        float,
        story: None,
        freeform: None,
        effects: Default::default(),
        extra,
    }
}

/// A diagram centred on its page, text wrapping above and below it (as Word's drawing canvases
/// in reports usually are).
fn centred(z: u32) -> Float {
    Float {
        wrap: Wrap::Square,
        h_rel: Anchor::Margin,
        v_rel: Anchor::Margin,
        h_align: Some(FloatAlign::Center),
        v_align: Some(FloatAlign::Center),
        z,
        ..Default::default()
    }
}

/// The body lines of page `pi`: (top-level block, top, bottom).
fn lines(l: &DocLayout, pi: usize) -> Vec<(usize, f32, f32)> {
    l.pages
        .get(pi)
        .map(|p| {
            p.items
                .iter()
                .filter_map(|it| match it {
                    Placed::Lines { story: StoryRef::Body, path, para, l0, l1, y, .. } => {
                        let h: f32 = para.lines.get(*l0..*l1).map_or(0.0, |ls| ls.iter().map(|l| l.height).sum());
                        Some((path.0.first().copied().unwrap_or(0) as usize, *y, *y + h))
                    }
                    _ => None,
                })
                .collect()
        })
        .unwrap_or_default()
}

/// The pages (and frames) a `h`-high shape is drawn on.
fn shapes_of_height(l: &DocLayout, h: f32) -> Vec<(usize, Rect)> {
    let mut out = Vec::new();
    for (pi, p) in l.pages.iter().enumerate() {
        for it in &p.items {
            if let Placed::Shape { rect, .. } = it
                && (rect.h - h).abs() < 0.01
            {
                out.push((pi, *rect));
            }
        }
    }
    out
}

/// A diagram centred on the page whose anchor comes after some of the page's lines pushes
/// those lines below it too, as Word does: no line of the page runs under it. (Wider than the
/// column, as the drawing canvases of reports often are, so no line fits beside it.)
#[test]
fn a_centred_diagram_wraps_the_lines_before_its_anchor() {
    let text: Vec<String> = (0..24).map(|i| format!("Line {i}")).collect();
    let mut d = Document::from_text(&text.join("\n"));
    let at = Pos::body(14, 0);
    d.insert_object(&at, shape(ShapeKind::Rectangle, 500.0, 300.0, centred(0), ShapeExtra::default()), &Default::default()).unwrap();
    let l = lay(&d);
    let found = shapes_of_height(&l, 300.0);
    let &[(pi, r)] = found.as_slice() else { panic!("{found:?}") };
    for (k, top, bottom) in lines(&l, pi) {
        assert!(bottom <= r.y + 0.5 || top >= r.bottom() - 0.5, "line of block {k} at {top}..{bottom} runs under the diagram at {}..{}", r.y, r.bottom());
    }
}

/// Two diagrams centred on the page, the second anchored after the first: the lines the
/// second pushes down push its own anchor onto the next page, and the diagram goes with it.
/// The first page then keeps its text where it was, with no hole for the diagram that left.
#[test]
fn a_second_centred_diagram_moves_to_the_next_page_with_its_anchor() {
    let text: Vec<String> = (0..60).map(|i| format!("Line {i}")).collect();
    let mut d = Document::from_text(&text.join("\n"));
    d.insert_object(&Pos::body(3, 0), shape(ShapeKind::Rectangle, 500.0, 250.0, centred(0), ShapeExtra::default()), &Default::default()).unwrap();
    d.insert_object(&Pos::body(16, 0), shape(ShapeKind::Rectangle, 500.0, 260.0, centred(0), ShapeExtra::default()), &Default::default()).unwrap();
    let l = lay(&d);
    let first = shapes_of_height(&l, 250.0);
    let second = shapes_of_height(&l, 260.0);
    assert_eq!(first.first().map(|f| f.0), Some(0), "{first:?}");
    let Some(&(pi, _)) = second.first() else { panic!("no second diagram") };
    assert_eq!(pi, 1, "the second diagram goes with its anchor to the next page");
    // Its anchor paragraph starts that page.
    let page2 = lines(&l, 1);
    assert_eq!(page2.first().map(|x| x.0), Some(16), "{page2:?}");
    // On the first page nothing runs under the first diagram, and the text after it runs on
    // below it without a gap left for the second.
    let r = first.first().map(|f| f.1).unwrap();
    let page1 = lines(&l, 0);
    for (k, top, bottom) in &page1 {
        assert!(*bottom <= r.y + 0.5 || *top >= r.bottom() - 0.5, "block {k} under the first diagram");
    }
    let last = page1.last().map(|x| x.0).unwrap();
    assert_eq!(last, 15, "page one ends with the line before the second anchor: {page1:?}");
}

/// The stacking order of overlapping objects anchored in one paragraph: a line the file puts
/// in front of a white box is drawn after it, though it comes first in the paragraph.
#[test]
fn overlapping_objects_draw_in_their_stacking_order() {
    let mut d = Document::from_text("Anchor\nAfter");
    let none = Float { wrap: Wrap::InFrontOfText, h_rel: Anchor::Column, v_rel: Anchor::Paragraph, x: 10.0, y: 10.0, ..Default::default() };
    let line = shape(ShapeKind::Line, 100.0, 0.0, Float { z: 3, y: 40.0, ..none.clone() }, ShapeExtra::default());
    let bx = shape(ShapeKind::Rectangle, 120.0, 90.0, Float { z: 2, ..none }, ShapeExtra::default());
    d.insert_object(&Pos::body(0, 0), line, &Default::default()).unwrap();
    let after_line = d.body.first().and_then(|b| b.as_para()).map_or(0, |p| p.text.char_indices().nth(1).map_or(p.len(), |(i, _)| i));
    d.insert_object(&Pos::body(0, after_line), bx, &Default::default()).unwrap();
    let l = lay(&d);
    let order: Vec<ShapeKind> = l.pages[0].items.iter().filter_map(|it| if let Placed::Shape { kind, .. } = it { Some(*kind) } else { None }).collect();
    assert_eq!(order, [ShapeKind::Rectangle, ShapeKind::Line], "the line (higher) is drawn over the box");
}

/// A text box that doesn't wrap (`wrap="none"`): a label wider than the box stays on one line.
#[test]
fn a_text_box_that_does_not_wrap_keeps_its_label_on_one_line() {
    for no_wrap in [false, true] {
        let mut d = Document::from_text("Anchor");
        let id = d.add_part(wordcraft_doc::PartKind::TextBox, vec![wordcraft_doc::para_block(Paragraph::with_text("Process Query", Default::default()))]);
        if let Some(p) = d.parts.get_mut(&id) {
            p.body.no_wrap = no_wrap;
        }
        let float = Float { wrap: Wrap::InFrontOfText, h_rel: Anchor::Column, v_rel: Anchor::Paragraph, ..Default::default() };
        let tb = InlineObject::Shape {
            kind: ShapeKind::TextBox,
            w: 30.0,
            h: 60.0,
            fill: None,
            stroke: None,
            stroke_width: 0.0,
            float,
            story: Some(id),
            freeform: None,
            effects: Default::default(),
            extra: ShapeExtra::default(),
        };
        d.insert_object(&Pos::body(0, 0), tb, &Default::default()).unwrap();
        let l = lay(&d);
        let n: usize = l.pages[0]
            .items
            .iter()
            .filter_map(|it| match it {
                Placed::Lines { story: StoryRef::Part(s), l0, l1, .. } if *s == id => Some(l1 - l0),
                _ => None,
            })
            .sum();
        if no_wrap {
            assert_eq!(n, 1, "one line");
        } else {
            assert!(n >= 2, "a 30 pt box wraps the label: {n}");
        }
    }
}

/// A tab whose default stop lies past the right margin moves to the next line and steps on
/// from its start there (labels lined up with runs of tabs, as Word lays them out).
#[test]
fn tabs_past_the_right_margin_carry_on_on_the_next_line() {
    // Letter, 1" margins: 468 pt of line, default stops every 36 pt. Text, then 19 tabs.
    let d = Document::from_text(&format!("Process{}Label", "\t".repeat(19)));
    let l = lay(&d);
    let p = d.body.first().and_then(|b| b.as_para()).unwrap();
    let start = l.caret(&Pos::body(0, 0)).unwrap();
    let label = p.text.find("Label").unwrap();
    let caret = l.caret(&Pos::body(0, label)).unwrap();
    // 12 stops after "Process" (to 468) on line one; the 13th moves to the next line at 36 pt
    // and the six left go on to 252 pt: the label starts 252 pt into its line.
    assert!((caret.x - (72.0 + 252.0)).abs() < 1.0, "label at {}", caret.x);
    assert!(caret.top > start.top + 1.0, "on a later line");
}

/// A line's arrowhead sits where its stroke ends: at the bottom of a line drawn downwards.
#[test]
fn a_line_arrowhead_points_the_way_the_line_runs() {
    let mut d = Document::from_text("Anchor");
    let float = Float { wrap: Wrap::InFrontOfText, h_rel: Anchor::Column, v_rel: Anchor::Paragraph, x: 50.0, y: 0.0, ..Default::default() };
    let extra = ShapeExtra { arrow_end: true, ..Default::default() };
    d.insert_object(&Pos::body(0, 0), shape(ShapeKind::Line, 0.0, 40.0, float, extra), &Default::default()).unwrap();
    let l = lay(&d);
    let rect = l.pages[0].items.iter().find_map(|it| if let Placed::Shape { rect, kind: ShapeKind::Line, .. } = it { Some(*rect) } else { None }).unwrap();
    let draws = display::page_display(&d, &l.pages[0], &Default::default());
    let tip = draws
        .iter()
        .find_map(|dr| match dr {
            display::Draw::Path { segs, fill: Some(_), .. } => match segs.first() {
                Some(wordcraft_doc::graphic::PathSeg::Move(x, y)) => Some((*x, *y)),
                _ => None,
            },
            _ => None,
        })
        .unwrap();
    // (A vertical line's frame is a point wide.)
    assert!((tip.1 - rect.bottom()).abs() < 0.5 && tip.0 >= rect.x - 0.5 && tip.0 <= rect.right() + 0.5, "tip {tip:?} for {rect:?}");
}

/// An elbow connector routed as its preset says: `bentConnector2` bends once, and its arrowhead
/// comes in along the last piece (down), not across.
#[test]
fn a_one_bend_elbow_comes_in_from_above() {
    let mut d = Document::from_text("Anchor");
    let float = Float { wrap: Wrap::InFrontOfText, h_rel: Anchor::Column, v_rel: Anchor::Paragraph, x: 50.0, y: 0.0, ..Default::default() };
    let extra = ShapeExtra { arrow_end: true, bent: Some(Bent { segments: 2, adj: [None; 3] }), ..Default::default() };
    d.insert_object(&Pos::body(0, 0), shape(ShapeKind::ElbowConnector, 60.0, 40.0, float, extra), &Default::default()).unwrap();
    let l = lay(&d);
    let draws = display::page_display(&d, &l.pages[0], &Default::default());
    let mut paths = draws.iter().filter_map(|dr| match dr {
        display::Draw::Path { segs, fill, .. } => Some((segs.clone(), fill.is_some())),
        _ => None,
    });
    let (route, _) = paths.find(|(_, filled)| !filled).unwrap();
    assert_eq!(route.len(), 3, "move and two pieces: {route:?}");
    let (head, _) = draws
        .iter()
        .filter_map(|dr| match dr {
            display::Draw::Path { segs, fill: Some(_), .. } => Some((segs.clone(), ())),
            _ => None,
        })
        .next()
        .unwrap();
    // The two barbs sit above the tip: the head points down.
    let ys: Vec<f32> = head
        .iter()
        .filter_map(|s| match s {
            wordcraft_doc::graphic::PathSeg::Move(_, y) | wordcraft_doc::graphic::PathSeg::Line(_, y) => Some(*y),
            _ => None,
        })
        .collect();
    assert!(ys.len() == 3 && ys[1] < ys[0] && ys[2] < ys[0], "{ys:?}");
}
