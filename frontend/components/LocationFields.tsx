"use client";

import { useEffect, useState, type Ref } from "react";
import {
  listPlatformCities,
  listPlatformStates,
  type PlatformCityItem,
  type PlatformStateItem,
} from "@/lib/api";
import { Field } from "@/components/ui/Field";
import { Select } from "@/components/ui/Select";

type LocationFieldsProps = {
  state: string;
  city: string;
  stateError?: string;
  cityError?: string;
  onStateChange: (value: string) => void;
  onCityChange: (value: string) => void;
  stateRef?: Ref<HTMLButtonElement>;
  cityRef?: Ref<HTMLButtonElement>;
  disabled?: boolean;
};

function withSavedOption(
  options: Array<{ value: string; label: string }>,
  saved: string,
) {
  if (!saved || options.some((option) => option.value === saved)) {
    return options;
  }
  return [{ value: saved, label: saved }, ...options];
}

export function LocationFields({
  state,
  city,
  stateError,
  cityError,
  onStateChange,
  onCityChange,
  stateRef,
  cityRef,
  disabled,
}: LocationFieldsProps) {
  const [states, setStates] = useState<PlatformStateItem[]>([]);
  const [cities, setCities] = useState<PlatformCityItem[]>([]);

  useEffect(() => {
    let cancelled = false;
    listPlatformStates()
      .then((items) => {
        if (!cancelled) setStates(items);
      })
      .catch(() => {
        if (!cancelled) setStates([]);
      });
    return () => {
      cancelled = true;
    };
  }, []);

  const selectedState = states.find((item) => item.name === state);

  useEffect(() => {
    if (!selectedState) {
      setCities([]);
      return;
    }

    let cancelled = false;
    listPlatformCities(selectedState.id)
      .then((items) => {
        if (!cancelled) setCities(items);
      })
      .catch(() => {
        if (!cancelled) setCities([]);
      });
    return () => {
      cancelled = true;
    };
  }, [selectedState?.id]);

  const stateOptions = withSavedOption(
    states.map((item) => ({ value: item.name, label: item.name })),
    state,
  );
  const cityOptions = withSavedOption(
    cities.map((item) => ({ value: item.name, label: item.name })),
    city,
  );

  return (
    <>
      <Field id="state" label="State" error={stateError}>
        <Select
          inputRef={stateRef}
          value={state}
          disabled={disabled}
          options={stateOptions}
          onChange={(next) => {
            onStateChange(next);
            if (next !== state) {
              onCityChange("");
            }
          }}
        />
      </Field>
      <Field id="city" label="City" error={cityError}>
        <Select
          inputRef={cityRef}
          value={city}
          disabled={disabled || !state}
          options={cityOptions}
          onChange={onCityChange}
        />
      </Field>
    </>
  );
}
