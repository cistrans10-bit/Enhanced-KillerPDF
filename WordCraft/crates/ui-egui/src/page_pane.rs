//! The Navigation pane's Pages tab, working like a PDF editor's page panel: a thumbnail of every
//! page; click one to go there (Shift+click picks a run of pages), drag pages to move them, and
//! duplicate, copy, cut, paste, delete or add pages from the toolbar, the right-click menu or the
//! keyboard (Mod+D, Mod+C, Mod+X, Mod+V, Delete, Alt+Up/Down, arrows) while a page is picked.
//! Everything runs through the `page.*` commands, so it undoes like any other edit.

use std::collections::HashMap;

use egui::{Color32, CornerRadius, Event, Key, Modifiers, Rect, Sense, Stroke, StrokeKind, Ui, pos2, vec2};
use serde_json::json;

use crate::WordApp;
use crate::theme::{Tokens, regular};

/// Widest a thumbnail is drawn, points.
const THUMB_MAX_W: f32 = 168.0;
/// Narrowest, points.
const THUMB_MIN_W: f32 = 56.0;
/// Thumbnails rendered per frame (the rest follow on the next frames).
const RENDERS_PER_FRAME: usize = 2;
/// Thumbnails kept around at most (pages far away are dropped first).
const MAX_CACHED: usize = 400;

/// A rendered page thumbnail and what it was rendered from.
struct Thumb {
    rev: u64,
    doc: u64,
    px: u32,
    dark: bool,
    tex: egui::TextureHandle,
}

/// The Pages tab's own state (not saved).
#[derive(Default)]
pub struct PagesUi {
    thumbs: HashMap<usize, Thumb>,
    /// Picked pages: where the pick started and where it ends now (0-based, either order).
    pick: Option<(usize, usize)>,
    /// Pages being dragged: the first and how many.
    drag: Option<(usize, usize)>,
    /// The slot a drop would put them in (0 = before the first page).
    drop_slot: Option<usize>,
    /// The page last scrolled into view for the caret, so the list follows the document.
    followed: Option<usize>,
}

impl PagesUi {
    /// The picked pages as first and count.
    fn picked(&self) -> Option<(usize, usize)> {
        self.pick.map(|(a, b)| (a.min(b), a.max(b) - a.min(b) + 1))
    }
    fn is_picked(&self, i: usize) -> bool {
        self.picked().is_some_and(|(f, n)| i >= f && i < f + n)
    }
}

pub fn show(app: &mut WordApp, ui: &mut Ui) {
    let t = Tokens::get(ui.ctx());
    let layout = app.session.layout();
    let pages = layout.pages.len();
    if let Some((a, b)) = app.pages_ui.pick
        && (a >= pages || b >= pages)
    {
        app.pages_ui.pick = Some((a.min(pages.saturating_sub(1)), b.min(pages.saturating_sub(1))));
    }
    let picked = app.pages_ui.picked();
    let target = picked.unwrap_or((app.session.page_hint.min(pages.saturating_sub(1)), 1));
    let has_clip = app.session.page_clipboard.is_some();

    // Toolbar: what the right-click menu offers, for the picked pages (else the caret's).
    ui.horizontal_wrapped(|ui| {
        ui.spacing_mut().item_spacing.x = 2.0;
        let (first, count) = target;
        let one = json!({"page": first + 1, "count": count});
        if crate::widgets::icon_button(ui, "duplicatePage", tl!("Duplicate Page")).clicked() {
            run(app, "page.duplicate", one.clone(), Some((first + count, count)));
        }
        if crate::widgets::icon_button(ui, "copy", tl!("Copy Page")).clicked() {
            run(app, "page.copy", one.clone(), None);
        }
        if crate::widgets::icon_button(ui, "cut", tl!("Cut Page")).clicked() {
            run(app, "page.cut", one.clone(), Some((first, 1)));
        }
        let paste = ui.add_enabled_ui(has_clip, |ui| crate::widgets::icon_button(ui, "paste", tl!("Paste After"))).inner;
        if paste.clicked() {
            run(app, "page.paste", json!({"after": first + count}), Some((first + count, 1)));
        }
        if crate::widgets::icon_button(ui, "blankPage", tl!("Insert Blank Page")).clicked() {
            run(app, "page.insertBlank", json!({"after": first + count}), Some((first + count, 1)));
        }
        if crate::widgets::icon_button(ui, "deletePage", tl!("Delete Page")).clicked() {
            run(app, "page.delete", one, Some((first, 1)));
        }
    });
    ui.label(egui::RichText::new(tl!("Drag a page to move it. Right-click for more.")).small().weak());
    ui.add_space(4.0);

    // Keys while a thumbnail has the keyboard.
    let thumb_id = |i: usize| egui::Id::new(("page_thumb", i));
    let focused = ui.ctx().memory(|m| m.focused()).is_some_and(|f| (0..pages).any(|i| thumb_id(i) == f));
    if focused {
        keys(app, ui, pages);
    }

    let ppp = ui.ctx().pixels_per_point();
    let width = (ui.available_width() - 28.0).clamp(THUMB_MIN_W, THUMB_MAX_W);
    let dark = app.session.view.dark_mode;
    let rev = app.session.rev();
    let doc_id = app.session.document_id();
    let mut budget = RENDERS_PER_FRAME;
    let mut slots: Vec<Rect> = Vec::with_capacity(pages);
    let mut stale = false;
    let caret_page = app.session.page_hint;
    let follow = app.pages_ui.followed != Some(caret_page) && app.pages_ui.drag.is_none();
    let mut clicked: Option<(usize, Modifiers)> = None;
    let mut context: Option<usize> = None;
    let mut drag_start: Option<usize> = None;
    let mut drag_end = false;

    egui::ScrollArea::vertical().id_salt("pages_scroll").auto_shrink([false, false]).show(ui, |ui| {
        let clip = ui.clip_rect();
        for (i, page) in layout.pages.iter().enumerate() {
            let aspect = if page.w > 0.0 { (page.h / page.w).clamp(0.1, 10.0) } else { 1.3 };
            let size = vec2(width, width * aspect);
            ui.vertical_centered(|ui| {
                let (rect, _) = ui.allocate_exact_size(size + vec2(8.0, 8.0), Sense::hover());
                let resp = ui.interact(rect, thumb_id(i), Sense::click_and_drag());
                slots.push(rect);
                let inner = Rect::from_center_size(rect.center(), size);
                let picked = app.pages_ui.is_picked(i);
                let current = i == caret_page;
                if picked || resp.hovered() {
                    let fill = if picked { t.accent.linear_multiply(0.18) } else { t.hover };
                    ui.painter().rect_filled(rect, CornerRadius::same(4), fill);
                }
                // The page: its thumbnail when one is ready, else plain paper.
                let paper = if dark { Color32::from_gray(wordcraft_render::DARK_PAPER) } else { Color32::WHITE };
                ui.painter().rect_filled(inner, 0.0, paper);
                let visible = clip.intersects(rect);
                let px = (width * ppp).round().max(1.0) as u32;
                let fresh = app.pages_ui.thumbs.get(&i).is_some_and(|th| th.rev == rev && th.doc == doc_id && th.px == px && th.dark == dark);
                if visible && !fresh {
                    if budget > 0 {
                        budget -= 1;
                        let mut opts = crate::canvas::screen_render_options();
                        opts.dark = dark;
                        opts.dark_paper = wordcraft_render::DARK_PAPER;
                        let scale = px as f32 / page.w.max(1.0);
                        let img = wordcraft_render::render_page(&app.session.doc, page, scale, &opts);
                        let image = egui::ColorImage::from_rgba_premultiplied([img.width as usize, img.height as usize], &img.pixels);
                        let tex = match app.pages_ui.thumbs.remove(&i) {
                            Some(mut th) => {
                                th.tex.set(image, egui::TextureOptions::LINEAR);
                                th.tex
                            }
                            None => ui.ctx().load_texture(format!("page_thumb_{i}"), image, egui::TextureOptions::LINEAR),
                        };
                        app.pages_ui.thumbs.insert(i, Thumb { rev, doc: doc_id, px, dark, tex });
                    } else {
                        stale = true;
                    }
                }
                if let Some(th) = app.pages_ui.thumbs.get(&i).filter(|th| th.doc == doc_id) {
                    ui.painter().image(th.tex.id(), inner, Rect::from_min_max(pos2(0.0, 0.0), pos2(1.0, 1.0)), Color32::WHITE);
                }
                let edge = if picked || current { Stroke::new(2.0, t.accent) } else { Stroke::new(1.0, t.border_strong) };
                ui.painter().rect_stroke(inner, 0.0, edge, StrokeKind::Outside);
                if follow && current {
                    ui.scroll_to_rect(rect, Some(egui::Align::Center));
                    app.pages_ui.followed = Some(caret_page);
                }
                let label = crate::i18n::fmt(tl!("Page {page}"), &[("page", &(i + 1).to_string())]);
                ui.label(egui::RichText::new(label).font(regular(11.5)).color(if picked { t.accent } else { t.text }));
                ui.add_space(4.0);
                if resp.clicked() {
                    clicked = Some((i, ui.input(|inp| inp.modifiers)));
                }
                if resp.secondary_clicked() {
                    context = Some(i);
                }
                if resp.drag_started() {
                    drag_start = Some(i);
                }
                if resp.drag_stopped() {
                    drag_end = true;
                }
                resp.context_menu(|ui| page_menu(app, ui, i, pages));
            });
        }
        // While dragging: where the pages would land, and scrolling near the edges.
        if app.pages_ui.drag.is_some()
            && let Some(p) = ui.ctx().pointer_interact_pos()
        {
            let slot = slots.iter().position(|r| p.y < r.center().y).unwrap_or(slots.len());
            app.pages_ui.drop_slot = Some(slot);
            let y = match (slot.checked_sub(1).and_then(|k| slots.get(k)), slots.get(slot)) {
                (Some(a), Some(b)) => (a.bottom() + b.top()) / 2.0 + 8.0,
                (Some(a), None) => a.bottom() + 10.0,
                (None, Some(b)) => b.top() - 2.0,
                (None, None) => clip.top(),
            };
            let x0 = slots.first().map_or(clip.left(), |r| r.left());
            let x1 = slots.first().map_or(clip.right(), |r| r.right());
            ui.painter().line_segment([pos2(x0, y), pos2(x1, y)], Stroke::new(3.0, t.accent));
            let edge = 36.0;
            if p.y < clip.top() + edge {
                ui.scroll_with_delta(vec2(0.0, 10.0));
            } else if p.y > clip.bottom() - edge {
                ui.scroll_with_delta(vec2(0.0, -10.0));
            }
            ui.ctx().set_cursor_icon(egui::CursorIcon::Grabbing);
            ui.ctx().request_repaint();
        }
    });
    if stale {
        ui.ctx().request_repaint();
    }
    // Thumbnails of pages that no longer exist go, and the cache stays bounded.
    if app.pages_ui.thumbs.len() > pages.min(MAX_CACHED) {
        let keep = caret_page;
        app.pages_ui.thumbs.retain(|i, _| *i < pages && i.abs_diff(keep) < MAX_CACHED / 2);
    }

    if let Some((i, mods)) = clicked {
        match app.pages_ui.pick {
            Some((a, _)) if mods.shift => app.pages_ui.pick = Some((a, i)),
            _ => app.pages_ui.pick = Some((i, i)),
        }
        ui.ctx().memory_mut(|m| m.request_focus(thumb_id(i)));
        let _ = app.run("edit.goto", json!({"page": i + 1}));
        app.pages_ui.followed = Some(i);
    }
    if let Some(i) = context
        && !app.pages_ui.is_picked(i)
    {
        app.pages_ui.pick = Some((i, i));
    }
    if let Some(i) = drag_start {
        // Dragging a picked page takes all the picked ones; another page goes alone.
        let what = match app.pages_ui.picked() {
            Some((f, n)) if i >= f && i < f + n => (f, n),
            _ => {
                app.pages_ui.pick = Some((i, i));
                (i, 1)
            }
        };
        app.pages_ui.drag = Some(what);
    }
    if drag_end || (app.pages_ui.drag.is_some() && !ui.input(|i| i.pointer.any_down())) {
        if let (Some((first, count)), Some(slot)) = (app.pages_ui.drag.take(), app.pages_ui.drop_slot.take()) {
            if slot < first || slot > first + count {
                let after = if slot > first { slot - count } else { slot };
                run(app, "page.move", json!({"page": first + 1, "count": count, "to": slot + 1}), Some((after, count)));
            }
        }
        app.pages_ui.drag = None;
        app.pages_ui.drop_slot = None;
    }
}

/// Run a page command and pick the pages it leaves at `pick` (first, count) afterwards.
fn run(app: &mut WordApp, id: &str, params: serde_json::Value, pick: Option<(usize, usize)>) {
    match app.run(id, params) {
        Ok(_) => {
            if let Some((first, count)) = pick {
                let pages = app.session.layout().pages.len();
                if pages > 0 {
                    let first = first.min(pages - 1);
                    let last = (first + count.max(1) - 1).min(pages - 1);
                    app.pages_ui.pick = Some((first, last));
                    app.pages_ui.followed = None;
                }
            }
        }
        Err(e) => app.session.status = e,
    }
}

/// The right-click menu of page `i`, for the picked pages it belongs to.
fn page_menu(app: &mut WordApp, ui: &mut Ui, i: usize, pages: usize) {
    let (first, count) = app.pages_ui.picked().filter(|(f, n)| i >= *f && i < f + n).unwrap_or((i, 1));
    let range = json!({"page": first + 1, "count": count});
    let has_clip = app.session.page_clipboard.is_some();
    if ui.button(tl!("Duplicate Page")).clicked() {
        run(app, "page.duplicate", range.clone(), Some((first + count, count)));
        ui.close();
    }
    ui.separator();
    if ui.button(tl!("Copy Page")).clicked() {
        run(app, "page.copy", range.clone(), None);
        ui.close();
    }
    if ui.button(tl!("Cut Page")).clicked() {
        run(app, "page.cut", range.clone(), Some((first, 1)));
        ui.close();
    }
    if ui.add_enabled(has_clip, egui::Button::new(tl!("Paste Before"))).clicked() {
        run(app, "page.paste", json!({"before": first + 1}), Some((first, 1)));
        ui.close();
    }
    if ui.add_enabled(has_clip, egui::Button::new(tl!("Paste After"))).clicked() {
        run(app, "page.paste", json!({"after": first + count}), Some((first + count, 1)));
        ui.close();
    }
    ui.separator();
    if ui.button(tl!("Insert Blank Page")).clicked() {
        run(app, "page.insertBlank", json!({"after": first + count}), Some((first + count, 1)));
        ui.close();
    }
    if ui.add_enabled(first > 0, egui::Button::new(tl!("Move Up"))).clicked() {
        run(app, "page.move", json!({"page": first + 1, "count": count, "to": first}), Some((first - 1, count)));
        ui.close();
    }
    if ui.add_enabled(first + count < pages, egui::Button::new(tl!("Move Down"))).clicked() {
        run(app, "page.move", json!({"page": first + 1, "count": count, "to": first + count + 2}), Some((first + 1, count)));
        ui.close();
    }
    ui.separator();
    if ui.button(tl!("Delete Page")).clicked() {
        run(app, "page.delete", range, Some((first, 1)));
        ui.close();
    }
}

/// Keyboard for the picked pages. The keys used are taken out of the frame's input, so the
/// document's own shortcuts (Copy, Font…) don't run as well.
fn keys(app: &mut WordApp, ui: &mut Ui, pages: usize) {
    let Some((first, count)) = app.pages_ui.picked() else { return };
    let range = json!({"page": first + 1, "count": count});
    let mut action: Option<&str> = None;
    let mut used = Vec::new();
    for (k, e) in ui.input(|i| i.events.clone()).into_iter().enumerate() {
        match &e {
            Event::Copy => {
                action = Some("copy");
                used.push(k);
            }
            Event::Cut => {
                action = Some("cut");
                used.push(k);
            }
            Event::Paste(_) => {
                action = Some("paste");
                used.push(k);
            }
            Event::Key { key, pressed, modifiers, .. } => {
                let cmd = modifiers.command && !modifiers.alt;
                let a = match key {
                    Key::C if cmd => Some("copy"),
                    Key::X if cmd => Some("cut"),
                    Key::V if cmd => Some("paste"),
                    Key::D if cmd => Some("duplicate"),
                    Key::Delete | Key::Backspace if modifiers.is_none() => Some("delete"),
                    Key::ArrowUp if modifiers.alt => Some("moveUp"),
                    Key::ArrowDown if modifiers.alt => Some("moveDown"),
                    Key::ArrowUp => Some(if modifiers.shift { "extendUp" } else { "up" }),
                    Key::ArrowDown => Some(if modifiers.shift { "extendDown" } else { "down" }),
                    Key::Escape => Some("leave"),
                    _ => None,
                };
                if let Some(a) = a {
                    used.push(k);
                    // Copy, Cut and Paste come as their own events too; the key press alone is enough.
                    if *pressed && action.is_none_or(|x| !matches!(x, "copy" | "cut" | "paste")) {
                        action = Some(a);
                    }
                }
            }
            _ => {}
        }
    }
    if used.is_empty() {
        return;
    }
    ui.input_mut(|i| {
        let mut k = 0;
        i.events.retain(|_| {
            let keep = !used.contains(&k);
            k += 1;
            keep
        });
    });
    let Some(action) = action else { return };
    let last = first + count - 1;
    match action {
        "copy" => run(app, "page.copy", range, None),
        "cut" => run(app, "page.cut", range, Some((first, 1))),
        "paste" if app.session.page_clipboard.is_some() => run(app, "page.paste", json!({"after": first + count}), Some((first + count, 1))),
        "duplicate" => run(app, "page.duplicate", range, Some((first + count, count))),
        "delete" => run(app, "page.delete", range, Some((first, 1))),
        "moveUp" if first > 0 => run(app, "page.move", json!({"page": first + 1, "count": count, "to": first}), Some((first - 1, count))),
        "moveDown" if last + 1 < pages => run(app, "page.move", json!({"page": first + 1, "count": count, "to": first + count + 2}), Some((first + 1, count))),
        "up" | "down" | "extendUp" | "extendDown" => {
            let (a, b) = app.pages_ui.pick.unwrap_or((first, first));
            let to = if action.ends_with("Up") || action == "up" { b.saturating_sub(1) } else { (b + 1).min(pages.saturating_sub(1)) };
            app.pages_ui.pick = Some(if action.starts_with("extend") { (a, to) } else { (to, to) });
            ui.ctx().memory_mut(|m| m.request_focus(egui::Id::new(("page_thumb", to))));
            let _ = app.run("edit.goto", json!({"page": to + 1}));
            app.pages_ui.followed = None;
        }
        "leave" => app.canvas.want_focus = true,
        _ => {}
    }
    // The thumbnail keeps the keyboard after an edit, as a PDF editor's page panel does.
    if let Some((f, _)) = app.pages_ui.picked()
        && action != "leave"
    {
        ui.ctx().memory_mut(|m| m.request_focus(egui::Id::new(("page_thumb", f))));
    }
}
