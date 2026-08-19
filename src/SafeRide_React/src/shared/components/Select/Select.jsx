import { Children, isValidElement, useEffect, useId, useMemo, useRef, useState } from 'react';
import { createPortal } from 'react-dom';
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';
import { faCheck, faChevronDown } from '@fortawesome/free-solid-svg-icons';
import './Select.css';

function Select({
  children,
  className = '',
  disabled = false,
  name,
  onBlur,
  onChange,
  value = '',
  'aria-invalid': ariaInvalid,
  'aria-label': ariaLabel,
}) {
  const options = useMemo(() => readOptions(children), [children]);
  const selectedIndex = Math.max(0, options.findIndex((option) => String(option.value) === String(value)));
  const selectedOption = options[selectedIndex];
  const [isOpen, setIsOpen] = useState(false);
  const [activeIndex, setActiveIndex] = useState(selectedIndex);
  const [menuPosition, setMenuPosition] = useState(null);
  const rootRef = useRef(null);
  const menuRef = useRef(null);
  const listboxId = useId();

  const updateMenuPosition = () => {
    const trigger = rootRef.current?.getBoundingClientRect();
    if (!trigger) return;
    const spaceBelow = window.innerHeight - trigger.bottom;
    const openUpward = spaceBelow < 260 && trigger.top > spaceBelow;
    const viewportPadding = 12;
    const menuWidth = Math.max(trigger.width, 220);
    const preferredLeft = trigger.width < menuWidth ? trigger.right - menuWidth : trigger.left;
    const left = Math.min(
      Math.max(preferredLeft, viewportPadding),
      window.innerWidth - menuWidth - viewportPadding,
    );
    setMenuPosition({
      left,
      top: openUpward ? undefined : trigger.bottom + 7,
      bottom: openUpward ? window.innerHeight - trigger.top + 7 : undefined,
      width: menuWidth,
    });
  };

  useEffect(() => {
    if (!isOpen) return undefined;
    const closeOnOutsideClick = (event) => {
      if (!rootRef.current?.contains(event.target) && !menuRef.current?.contains(event.target)) {
        setIsOpen(false);
      }
    };
    const reposition = () => updateMenuPosition();
    document.addEventListener('pointerdown', closeOnOutsideClick);
    window.addEventListener('resize', reposition);
    window.addEventListener('scroll', reposition, true);
    return () => {
      document.removeEventListener('pointerdown', closeOnOutsideClick);
      window.removeEventListener('resize', reposition);
      window.removeEventListener('scroll', reposition, true);
    };
  }, [isOpen]);

  const openMenu = () => {
    updateMenuPosition();
    setActiveIndex(selectedIndex);
    setIsOpen(true);
  };

  const chooseOption = (option) => {
    if (!option || option.disabled) return;
    onChange?.({ target: { name, value: option.value } });
    setActiveIndex(options.indexOf(option));
    setIsOpen(false);
  };

  const toggleMenu = () => {
    if (isOpen) setIsOpen(false);
    else openMenu();
  };

  const moveActive = (direction) => {
    if (!options.length) return;
    let nextIndex = activeIndex;
    do {
      nextIndex = (nextIndex + direction + options.length) % options.length;
    } while (options[nextIndex]?.disabled && nextIndex !== activeIndex);
    setActiveIndex(nextIndex);
  };

  const handleKeyDown = (event) => {
    if (disabled) return;
    if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
      event.preventDefault();
      if (!isOpen) openMenu();
      else moveActive(event.key === 'ArrowDown' ? 1 : -1);
      return;
    }
    if (event.key === 'Home' || event.key === 'End') {
      event.preventDefault();
      if (!isOpen) openMenu();
      setActiveIndex(event.key === 'Home' ? 0 : options.length - 1);
      return;
    }
    if (event.key === 'Enter' || event.key === ' ') {
      event.preventDefault();
      if (isOpen) chooseOption(options[activeIndex]);
      else openMenu();
      return;
    }
    if (event.key === 'Escape' && isOpen) {
      event.preventDefault();
      setIsOpen(false);
    }
  };

  return (
    <div
      className={`shared-select ${isOpen ? 'shared-select--open' : ''} ${className}`.trim()}
      ref={rootRef}
    >
      {name && <input type="hidden" name={name} value={value} />}
      <button
        type="button"
        className="shared-select__trigger"
        aria-controls={listboxId}
        aria-expanded={isOpen}
        aria-haspopup="listbox"
        aria-invalid={ariaInvalid}
        aria-label={ariaLabel}
        disabled={disabled}
        onBlur={onBlur}
        onClick={toggleMenu}
        onKeyDown={handleKeyDown}
      >
        <span>{selectedOption?.label ?? 'Chọn giá trị'}</span>
        <FontAwesomeIcon icon={faChevronDown} />
      </button>

      {isOpen && menuPosition && createPortal(
        <div
          className="shared-select__menu"
          id={listboxId}
          role="listbox"
          ref={menuRef}
          style={menuPosition}
        >
          {options.map((option, index) => (
            <button
              type="button"
              className={`shared-select__option ${index === activeIndex ? 'shared-select__option--active' : ''}`.trim()}
              role="option"
              aria-selected={String(option.value) === String(value)}
              disabled={option.disabled}
              key={`${option.value}-${index}`}
              onMouseEnter={() => setActiveIndex(index)}
              onMouseDown={(event) => event.preventDefault()}
              onClick={() => chooseOption(option)}
            >
              <span>{option.label}</span>
              {String(option.value) === String(value) && <FontAwesomeIcon icon={faCheck} />}
            </button>
          ))}
        </div>,
        document.body,
      )}
    </div>
  );
}

function readOptions(children) {
  return Children.toArray(children)
    .filter(isValidElement)
    .map((option) => ({
      value: option.props.value ?? '',
      label: option.props.children,
      disabled: Boolean(option.props.disabled),
    }));
}

export default Select;
