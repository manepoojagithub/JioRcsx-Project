document.addEventListener('DOMContentLoaded', function () {
    const form = document.getElementById('templateForm');
    const tabs = document.querySelectorAll('.tab-btn');
    const msgTypeInput = document.getElementById('selectedMessageType');
    const panels = document.querySelectorAll('.panel-content');
    const previewContent = document.getElementById('livePreviewContent');
    const addCardBtn = document.getElementById('addCarouselCard');
    const carouselContainer = document.getElementById('carouselCardsContainer');

    // Tab switching
    tabs.forEach(tab => {
        tab.addEventListener('click', () => {
            const type = tab.dataset.type;
            msgTypeInput.value = type;
            tabs.forEach(t => t.classList.remove('active'));
            tab.classList.add('active');
            
            showPanel(type);

            // Initialize carousel with 2 cards if empty
            if (type === 'Carousel' && carouselContainer.children.length === 0) {
                addCard();
                addCard();
            }
            updatePreview();
        });
    });

    function showPanel(type) {
        panels.forEach(p => {
            p.classList.add('d-none');
            if (p.dataset.panel === type) p.classList.remove('d-none');
        });
    }

    // Dynamic Carousel Cards
    if (addCardBtn) {
        addCardBtn.addEventListener('click', addCard);
    }

    function addCard() {
        const index = carouselContainer.children.length;
        if (index >= 10) {
            alert('Maximum 10 cards allowed.');
            return;
        }

        const template = `
            <div class="card-editor" data-index="${index}">
                <div class="card-editor-header">
                    <h4 class="h6 mb-0">Card #${index + 1}</h4>
                    <button type="button" class="btn btn-sm btn-link text-danger remove-card">Remove</button>
                </div>
                <div class="mb-3">
                    <label class="form-label small">Media File</label>
                    <input type="file" name="Cards[${index}].MediaFile" class="form-control form-control-sm media-input" accept="image/*,video/*" />
                    <input type="hidden" name="Cards[${index}].MediaUrl" class="media-url-val" />
                </div>
                <div class="mb-3">
                    <label class="form-label small">Title</label>
                    <input type="text" name="Cards[${index}].Title" class="form-control form-control-sm title-input" maxlength="200" placeholder="Card Title" />
                </div>
                <div class="mb-3">
                    <label class="form-label small">Description</label>
                    <textarea name="Cards[${index}].Description" class="form-control form-control-sm desc-input" rows="3" maxlength="2000" placeholder="Card Description"></textarea>
                </div>
                <div class="cta-container">
                    <label class="form-label small d-flex justify-content-between">
                        <span>Buttons</span>
                        <button type="button" class="btn btn-link btn-sm p-0 add-cta" data-card-index="${index}">Add Button</button>
                    </label>
                    <div class="ctas-list" data-card-index="${index}"></div>
                </div>
            </div>
        `;

        const div = document.createElement('div');
        div.innerHTML = template.trim();
        carouselContainer.appendChild(div.firstChild);
        updatePreview();
    }

    // Delegation for dynamic elements
    document.addEventListener('click', function(e) {
        if (e.target.classList.contains('remove-card')) {
            e.target.closest('.card-editor').remove();
            reindexCards();
            updatePreview();
        }
        if (e.target.classList.contains('add-cta')) {
            addCta(e.target.dataset.cardIndex);
        }
        if (e.target.classList.contains('remove-cta')) {
            const list = e.target.closest('.ctas-list');
            e.target.closest('.cta-item').remove();
            reindexCtas(list);
            updatePreview();
        }
    });

    function addCta(cardIndex) {
        const list = document.querySelector(`.ctas-list[data-card-index="${cardIndex}"]`);
        const index = list.children.length;
        if (index >= 4) return;

        const template = `
            <div class="cta-item mb-2 p-2 border rounded bg-light" data-cta-index="${index}">
                <div class="d-flex justify-content-between align-items-center mb-1">
                    <span class="x-small fw-bold">Button ${index + 1}</span>
                    <button type="button" class="btn btn-link btn-sm text-danger p-0 remove-cta">Delete</button>
                </div>
                <div class="row g-2">
                    <div class="col-6"><input type="text" name="Cards[${cardIndex}].Ctas[${index}].Text" class="form-control form-control-sm cta-text" placeholder="Text" required /></div>
                    <div class="col-6">
                        <select name="Cards[${cardIndex}].Ctas[${index}].ActionType" class="form-select form-select-sm cta-type">
                            <option value="OpenUrl">OpenUrl</option>
                            <option value="Dialer">Dialer</option>
                        </select>
                    </div>
                    <div class="col-12"><input type="text" name="Cards[${cardIndex}].Ctas[${index}].Value" class="form-control form-control-sm cta-value" placeholder="URL/Phone" required /></div>
                </div>
            </div>
        `;
        const div = document.createElement('div');
        div.innerHTML = template.trim();
        list.appendChild(div.firstChild);
        updatePreview();
    }

    function reindexCards() {
        Array.from(carouselContainer.children).forEach((card, idx) => {
            card.dataset.index = idx;
            card.querySelector('.h6').innerText = `Card #${idx + 1}`;
            card.querySelectorAll('[name*="Cards["]').forEach(input => {
                input.name = input.name.replace(/Cards\[\d+\]/, `Cards[${idx}]`);
            });
            card.querySelector('.add-cta').dataset.cardIndex = idx;
            card.querySelector('.ctas-list').dataset.cardIndex = idx;
        });
    }

    function reindexCtas(list) {
        const cardIdx = list.dataset.cardIndex;
        Array.from(list.children).forEach((cta, idx) => {
            cta.dataset.ctaIndex = idx;
            cta.querySelector('.fw-bold').innerText = `Button ${idx + 1}`;
            cta.querySelectorAll('[name*=".Ctas["]').forEach(input => {
                input.name = input.name.replace(/\.Ctas\[\d+\]/, `.Ctas[${idx}]`);
            });
        });
    }

    // Live Preview Logic
    function updatePreview() {
        if (!msgTypeInput) return;
        const type = msgTypeInput.value;
        let html = '';

        if (type === 'PlainText') {
            const textEl = document.getElementById('Text');
            const text = (textEl && textEl.value) ? textEl.value : 'Hello! This is your plain text preview...';
            html = `<div class="preview-bubble">${text.replace(/\n/g, '<br>')}</div>`;
        } else if (type === 'StandaloneCard') {
            const panel = document.querySelector('[data-panel="StandaloneCard"]');
            if (panel) html = renderCardPreview(panel.querySelector('.card-editor'));
        } else if (type === 'Carousel') {
            html = '<div class="preview-carousel">';
            if (carouselContainer) {
                Array.from(carouselContainer.children).forEach(card => {
                    html += `<div class="preview-carousel-item">${renderCardPreview(card)}</div>`;
                });
            }
            html += '</div>';
        }

        if (previewContent) previewContent.innerHTML = html;
    }

    function renderCardPreview(cardEl) {
        if (!cardEl) return '<div class="preview-card p-3 text-center text-muted small">Card setup in progress...</div>';
        const titleEl = cardEl.querySelector('.title-input');
        const descEl = cardEl.querySelector('.desc-input');
        const mediaUrlEl = cardEl.querySelector('.media-url-val');
        
        const title = titleEl ? titleEl.value : '';
        const desc = descEl ? descEl.value : '';
        const mediaUrl = mediaUrlEl ? mediaUrlEl.value : '';

        const ctas = Array.from(cardEl.querySelectorAll('.cta-item')).map(item => ({
            text: item.querySelector('.cta-text').value || 'Button'
        }));

        let ctaHtml = '';
        ctas.forEach(cta => {
            ctaHtml += `<div class="preview-btn">${cta.text}</div>`;
        });

        let mediaHtml = '<div class="preview-media"></div>';
        if (mediaUrl) {
            mediaHtml = `<div class="preview-media"><img src="${mediaUrl}" style="width:100%;height:100%;object-fit:cover;" /></div>`;
        }

        return `
            <div class="preview-card">
                ${mediaHtml}
                <div class="preview-body">
                    ${title ? `<div class="preview-title">${title}</div>` : '<div class="preview-title text-muted opacity-25">Title Here</div>'}
                    ${desc ? `<div class="preview-desc">${desc}</div>` : '<div class="preview-desc text-muted opacity-25">Description will appear here...</div>'}
                </div>
                <div class="preview-btns">${ctaHtml}</div>
            </div>
        `;
    }

    // Input listeners
    document.addEventListener('input', function(e) {
        if (e.target.tagName === 'INPUT' || e.target.tagName === 'TEXTAREA') {
            updatePreview();
        }
    });

    // File validation
    document.addEventListener('change', function(e) {
        if (e.target.type === 'file') {
            const file = e.target.files[0];
            if (file) {
                const maxSize = file.type.startsWith('video/') ? 10 * 1024 * 1024 : 2 * 1024 * 1024;
                if (file.size > maxSize) {
                    alert(`File too large. Max size for ${file.type.startsWith('video/') ? 'video is 10MB' : 'image is 2MB'}.`);
                    e.target.value = '';
                }
            }
        }
    });

    // Initialize panels based on current value
    if (msgTypeInput && msgTypeInput.value) {
        showPanel(msgTypeInput.value);
    } else if (msgTypeInput) {
        msgTypeInput.value = 'PlainText';
        showPanel('PlainText');
    }

    // Initial preview
    updatePreview();
});
