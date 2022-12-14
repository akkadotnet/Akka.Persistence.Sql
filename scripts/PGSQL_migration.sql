drop table if exists public.TagTable;

drop procedure if exists public.Split;
drop procedure if exists public."Normalize";

create table public.TagTable(
  ordering_id bigserial not null,
  tag VARCHAR(64) not null,
  primary key (ordering_id, tag)
);


create procedure public.Split(id bigint, tags varchar(8000))
as $$
declare var_t record;
begin
	for var_t in(select unnest(string_to_array(tags, ';')) as t)
	loop 
		continue when var_t.t is null or var_t.t = '';
		insert into public.tagtable (ordering_id, tag) values (id, var_t.t);
	end loop;
end;
$$ LANGUAGE plpgsql;

create procedure public."Normalize"() 
as $$
declare var_r record;
begin
	for var_r in(select "ordering", tags from event_journal order by "ordering")
	loop 
		call public.Split(var_r."ordering", var_r.tags);
	end loop;
end;
$$ LANGUAGE plpgsql;

call public."Normalize"();